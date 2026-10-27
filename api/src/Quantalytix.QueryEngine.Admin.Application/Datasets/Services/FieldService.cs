using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Lists, creates, edits and deletes the fields of dataset versions.</summary>
public sealed class FieldService(ITableGateway<FieldRecord> fields, FieldUsage usage, IIdGenerator ids)
{
    /// <summary>Lists the fields of a dataset version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The fields, sorted by key.</returns>
    public async Task<IReadOnlyList<FieldRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await fields.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        return [.. rows.OrderBy(row => row.FieldKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Creates a field in a draft dataset version, without a materialized column.</summary>
    /// <param name="versionId">The draft version.</param>
    /// <param name="input">The new field.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new field.</returns>
    public async Task<FieldRecord> CreateAsync(long versionId, FieldInput input, CancellationToken cancellationToken)
    {
        var field = ToRecord(ids.NextId(), versionId, input.FieldKey, null, input);
        await fields.InsertAsync(field, cancellationToken);
        return field;
    }

    /// <summary>
    /// Replaces a field's editable attributes. A materialized column holds the value the field's source definition computes,
    /// so changing the source kind, source column, JSON path, expression or type clears it.
    /// </summary>
    /// <param name="reference">The field and its draft version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The updated field.</returns>
    public async Task<FieldRecord> UpdateAsync(FieldReference reference, FieldUpdateInput input, CancellationToken cancellationToken)
    {
        var current = await fields.GetAsync(new { reference.FieldId }, cancellationToken);
        var updated = ToRecord(current.FieldId, current.DatasetVersionId, current.FieldKey, current.MaterializedColumn, input);
        if (!SameSource(current, updated)) updated = updated with { MaterializedColumn = null };
        await fields.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Deletes a field that no grain, binding, time binding or measure component references.</summary>
    /// <param name="reference">The field and its draft version.</param>
    /// <param name="cancellationToken">Cancels the reads and the delete.</param>
    /// <returns>Null when the field was deleted; otherwise the detail naming the rows that reference it.</returns>
    public async Task<string?> DeleteAsync(FieldReference reference, CancellationToken cancellationToken)
    {
        var field = await fields.GetAsync(new { reference.FieldId }, cancellationToken);
        var blockers = new DeleteBlockers($"Field '{field.FieldKey}'");
        blockers.AddNamed("field_in_use", "it is used by", await usage.ListAsync(field.FieldId, cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        await fields.DeleteAsync(new { reference.FieldId }, cancellationToken);
        return null;
    }

    /// <summary>A field row holding the input's attributes.</summary>
    private static FieldRecord ToRecord(long fieldId, long versionId, string fieldKey, string? materializedColumn, FieldUpdateInput input)
    {
        return new FieldRecord
        {
            FieldId = fieldId,
            DatasetVersionId = versionId,
            FieldKey = fieldKey,
            DisplayName = input.DisplayName,
            FieldSourceKind = input.FieldSourceKind,
            SourceColumn = input.SourceColumn,
            JsonPath = input.JsonPath,
            MaterializedColumn = materializedColumn,
            ExpressionText = input.ExpressionText,
            DataType = input.DataType,
            NumericPrecision = input.NumericPrecision,
            NumericScale = input.NumericScale,
            IsNullable = input.IsNullable,
            UnitKey = input.UnitKey,
            IsSelectable = input.IsSelectable,
            IsFilterable = input.IsFilterable,
            IsSortable = input.IsSortable,
            IsGroupable = input.IsGroupable,
            IsEnabled = input.IsEnabled
        };
    }

    /// <summary>Whether two versions of a field compute the same value, so a materialized column still holds it.</summary>
    private static bool SameSource(FieldRecord current, FieldRecord updated)
    {
        return current.FieldSourceKind == updated.FieldSourceKind
            && current.SourceColumn == updated.SourceColumn
            && current.JsonPath == updated.JsonPath
            && current.ExpressionText == updated.ExpressionText
            && current.DataType == updated.DataType
            && current.NumericPrecision == updated.NumericPrecision
            && current.NumericScale == updated.NumericScale;
    }
}
