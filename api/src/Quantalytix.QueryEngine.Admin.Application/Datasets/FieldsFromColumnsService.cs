using System.Collections.Frozen;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Creates <c>column</c> fields from the physical columns of a version's source object, named and typed from the columns,
/// so a dataset over a wide table is set up in one step.
/// </summary>
public sealed class FieldsFromColumnsService(
    ITableGateway<FieldRecord> fields,
    ITableGateway<DatasetVersionRecord> versions,
    SourceObjectService sourceObjects,
    IIdGenerator ids)
{
    /// <summary>The data types whose fields group directly by default: discrete values that make readable groups.</summary>
    private static readonly FrozenSet<string> GroupableDataTypes = FrozenSet.Create(
        StringComparer.Ordinal,
        DataTypes.String,
        DataTypes.Boolean,
        DataTypes.Int32,
        DataTypes.Int64,
        DataTypes.Date);

    /// <summary>
    /// Creates one <c>column</c> field per selected column of the version's source object, in column order. The key is the
    /// column name in camelCase, the display name the column name in Title Case, and the type the column's mapped type. The
    /// field is groupable for string, boolean, integer and date types; every other flag takes the schema default. Columns
    /// without a logical type, and columns that a <c>column</c> field of the version already reads, are skipped.
    /// </summary>
    /// <param name="versionId">The draft version.</param>
    /// <param name="input">The selected column names.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The new fields.</returns>
    public async Task<IReadOnlyList<FieldRecord>> CreateAsync(long versionId, FieldsFromColumnsInput input, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var columns = await sourceObjects.ListColumnsAsync(version.SourceObject, cancellationToken);
        var existing = await fields.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var readColumns = existing
            .Where(field => field.FieldSourceKind == FieldSourceKinds.Column)
            .Select(field => field.SourceColumn!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = input.Columns.ToHashSet(StringComparer.Ordinal);
        List<FieldRecord> created =
        [
            .. columns
                .Where(column => selected.Contains(column.Name) && column.DataType is not null && !readColumns.Contains(column.Name))
                .Select(column => FieldOf(versionId, column))
        ];
        await fields.InsertManyAsync(created, cancellationToken);
        return created;
    }

    /// <summary>A new <c>column</c> field over a typed column.</summary>
    private FieldRecord FieldOf(long versionId, SourceColumn column)
    {
        return new FieldRecord
        {
            FieldId = ids.NextId(),
            DatasetVersionId = versionId,
            FieldKey = ColumnNames.ToFieldKey(column.Name),
            DisplayName = ColumnNames.ToDisplayName(column.Name),
            FieldSourceKind = FieldSourceKinds.Column,
            SourceColumn = column.Name,
            DataType = column.DataType!,
            NumericPrecision = column.NumericPrecision,
            NumericScale = column.NumericScale,
            IsNullable = true,
            IsSelectable = true,
            IsFilterable = true,
            IsSortable = true,
            IsGroupable = GroupableDataTypes.Contains(column.DataType!),
            IsEnabled = true
        };
    }
}
