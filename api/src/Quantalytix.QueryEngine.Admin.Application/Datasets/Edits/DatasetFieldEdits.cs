using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The field operations of the dataset change set: keys fixed and unique per version (D4), column and JSON path fields reading a
/// column fields can read (D5), new columns only on managed tables (OI-33), and a materialized column dropped when the field's
/// source changes. A field that grains, bindings or measure components name is not deleted.
/// </summary>
internal static class DatasetFieldEdits
{
    /// <summary><c>saveField</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the field saved.</returns>
    public static DatasetSnapshot Save(DatasetSnapshot snapshot, DatasetSaveFieldOperation operation, ChangeContext context)
    {
        var input = operation.Input;
        var fieldId = DatasetEditTargets.SavedId(input.Id, context);
        var existing = snapshot.Fields.SingleOrDefault(field => field.FieldId == fieldId);
        if (DatasetEditTargets.IsGone(input.Id, existing is not null, "Field", context)) return snapshot;
        var others = snapshot.Fields.Where(field => field.FieldId != fieldId).Select(field => field.FieldKey);
        if (DatasetEditTargets.IsKeyRefused(existing?.FieldKey, input.Key, others, "field", context)) return snapshot;

        var saved = new FieldRecord
        {
            FieldId = fieldId,
            DatasetVersionId = snapshot.VersionId,
            FieldKey = input.Key,
            DisplayName = input.DisplayName.Trim(),
            FieldSourceKind = input.FieldSourceKind,
            SourceColumn = input.SourceColumn,
            JsonPath = input.JsonPath,
            MaterializedColumn = existing?.MaterializedColumn,
            ExpressionText = input.ExpressionText,
            DataType = input.DataType,
            NumericPrecision = input.NumericPrecision,
            NumericScale = input.NumericScale,
            IsNullable = input.IsNullable,
            UnitKey = string.IsNullOrWhiteSpace(input.UnitKey) ? null : input.UnitKey.Trim(),
            IsSelectable = input.IsSelectable,
            IsFilterable = input.IsFilterable,
            IsSortable = input.IsSortable,
            IsGroupable = input.IsGroupable,
            IsEnabled = input.IsEnabled
        };
        var sourceChanged = existing is null || !SameSource(existing, saved);
        if (sourceChanged) saved = saved with { MaterializedColumn = null };

        var next = snapshot;
        if (input.CreateColumn == true) next = WithNewColumn(snapshot, saved, context);
        else if (sourceChanged && saved.FieldSourceKind != FieldSourceKinds.Expression) CheckColumn(snapshot, saved, context);
        if (context.Refused) return snapshot;
        return next with { Fields = [.. next.Fields.Where(field => field.FieldId != fieldId), saved] };
    }

    /// <summary><c>deleteField</c>: refused while an item names the field by id.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate without the field.</returns>
    public static DatasetSnapshot Delete(DatasetSnapshot snapshot, DatasetDeleteFieldOperation operation, ChangeContext context)
    {
        if (DatasetEditTargets.Find(snapshot.Fields, field => field.FieldId, operation.FieldId, "Field", context) is not { } field) return snapshot;
        var check = DatasetDeleteRules.Item(snapshot, field.FieldId, $"Field '{field.FieldKey}'", DatasetItemBlockerCodes.FieldInUse);
        if (!check.Allowed)
        {
            context.Blocked(check);
            return snapshot;
        }

        return snapshot with { Fields = [.. snapshot.Fields.Where(row => row.FieldId != field.FieldId)] };
    }

    /// <summary>
    /// <c>addFieldsFromColumns</c>: one column field per named column that fields can read and no column field reads yet, keyed and
    /// named after the column. Each new field's temporary id follows from the operation's position and the column, so every replay of
    /// the same operations names the same fields.
    /// </summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the new fields.</returns>
    public static DatasetSnapshot AddFromColumns(DatasetSnapshot snapshot, DatasetAddFieldsFromColumnsOperation operation, ChangeContext context)
    {
        var read = snapshot.Fields
            .Where(field => field.FieldSourceKind == FieldSourceKinds.Column)
            .Select(field => field.SourceColumn!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keys = snapshot.Fields.Select(field => field.FieldKey).ToHashSet(StringComparer.Ordinal);
        var created = new List<FieldRecord>();
        for (var index = 0; index < operation.Columns.Count; index++)
        {
            var name = operation.Columns[index];
            var path = string.Create(CultureInfo.InvariantCulture, $"columns[{index}]");
            var column = ColumnOf(snapshot, name);
            var type = column is null ? null : ColumnTypeMapping.Map(column.ColumnType);
            if (column is null) context.Reject(path, $"'{name}' is not a column of {snapshot.Version.SourceObject}.");
            else if (type is null) context.Reject(path, $"Fields cannot read {column.ColumnType} columns.");
            else if (read.Contains(name)) context.Reject(path, $"'{name}' already backs a field.");
            if (column is null || type is null || context.Refused) continue;

            read.Add(column.Name);
            var fieldId = context.Ids.Resolve(string.Create(CultureInfo.InvariantCulture, $"{TemporaryIds.Prefix}columns-{context.OperationIndex}-{column.Name}"));
            created.Add(ColumnFields.Of(fieldId, snapshot.VersionId, ColumnFields.KeyOf(column.Name, keys), column.Name, type, column.IsNullable));
        }

        return context.Refused ? snapshot : snapshot with { Fields = [.. snapshot.Fields, .. created] };
    }

    /// <summary>
    /// The snapshot with a managed table's new column for a field, or a refusal when the table is not managed or has a column of that
    /// name and another type. A column of that name and the field's type is this operation's own DDL, which the commit runs before it
    /// replays the operations.
    /// </summary>
    private static DatasetSnapshot WithNewColumn(DatasetSnapshot snapshot, FieldRecord field, ChangeContext context)
    {
        if (snapshot.ManagedSource is not { SourceObjectKind: SourceObjectKinds.Table })
        {
            context.Conflict(DatasetRefusals.NotManagedTitle, $"'{snapshot.Version.SourceObject}' was not created by the API, so fields cannot add columns to it.");
            return snapshot;
        }

        var columnType = ColumnTypeMapping.ColumnTypeOf(field.DataType, field.NumericPrecision, field.NumericScale);
        if (ColumnOf(snapshot, field.SourceColumn!) is { } existing)
        {
            if (ColumnTypeMapping.Map(existing.ColumnType) != ColumnTypeMapping.Map(columnType))
                context.Reject("input.sourceColumn", $"'{field.SourceColumn}' already exists as {existing.ColumnType}; choose it from the columns instead.");
            return snapshot;
        }

        return DatasetSettingsEdits.WithColumn(snapshot, new PhysicalColumn { Name = field.SourceColumn!, ColumnType = columnType, IsNullable = field.IsNullable, IsGenerated = false });
    }

    /// <summary>Refuses a column or JSON path field whose column the source object lacks or fields cannot read as it needs.</summary>
    private static void CheckColumn(DatasetSnapshot snapshot, FieldRecord field, ChangeContext context)
    {
        var column = ColumnOf(snapshot, field.SourceColumn!);
        var type = column is null ? null : ColumnTypeMapping.Map(column.ColumnType);
        if (column is null) context.Reject("input.sourceColumn", $"'{field.SourceColumn}' is not a column of {snapshot.Version.SourceObject}.");
        else if (type is null) context.Reject("input.sourceColumn", $"Fields cannot read {column.ColumnType} columns.");
        else if (field.FieldSourceKind == FieldSourceKinds.JsonPath && type.DataType != DataTypes.Json) context.Reject("input.sourceColumn", "A JSON path reads a json column.");
    }

    /// <summary>A column of the version's source object; StarRocks column names ignore case.</summary>
    private static PhysicalColumn? ColumnOf(DatasetSnapshot snapshot, string name)
    {
        return snapshot.SourceColumns.SingleOrDefault(column => string.Equals(column.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a field still reads the same value, so its materialized column stays valid.</summary>
    private static bool SameSource(FieldRecord current, FieldRecord saved)
    {
        return current.FieldSourceKind == saved.FieldSourceKind
            && current.SourceColumn == saved.SourceColumn
            && current.JsonPath == saved.JsonPath
            && current.ExpressionText == saved.ExpressionText
            && current.DataType == saved.DataType
            && current.NumericPrecision == saved.NumericPrecision
            && current.NumericScale == saved.NumericScale;
    }
}
