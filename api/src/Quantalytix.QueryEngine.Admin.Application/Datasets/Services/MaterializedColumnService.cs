using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Points <c>json_path</c> and <c>expression</c> fields at physical columns that hold their values, so the query engine
/// reads the column instead of computing the value: a generated column added to the source table for a <c>json_path</c>
/// field, or a column the source object already has. Expression fields can only use existing columns, because the
/// engine's expression-to-SQL renderer is internal to its provider.
/// </summary>
public sealed class MaterializedColumnService(
    ITableGateway<FieldRecord> fields,
    ITableGateway<DatasetVersionRecord> versions,
    SourceObjectService sourceObjects,
    ISchemaChangeExecutor schemaChanges)
{
    /// <summary>
    /// Adds a generated column holding a <c>json_path</c> field's value to the version's source table, waits for the schema
    /// change, then sets the draft field's materialized column. Every version that reads the table shares the column, but
    /// only this draft field points at it, so published versions are unaffected.
    /// </summary>
    /// <param name="reference">The field and its draft version.</param>
    /// <param name="input">The new column's name.</param>
    /// <param name="cancellationToken">Cancels the reads, the schema change wait and the write.</param>
    /// <returns>
    /// Null when the column was added and set; otherwise why not: the field is not a <c>json_path</c> field, the source object
    /// is a view, or the table already has the column.
    /// </returns>
    public async Task<string?> MaterializeAsync(FieldReference reference, MaterializeInput input, CancellationToken cancellationToken)
    {
        var field = await fields.GetAsync(new { reference.FieldId }, cancellationToken);
        var version = await versions.GetAsync(new { DatasetVersionId = reference.VersionId }, cancellationToken);
        if (field.FieldSourceKind != FieldSourceKinds.JsonPath)
            return $"Field '{field.FieldKey}' is a {field.FieldSourceKind} field; only json_path fields are materialized as generated columns.";
        if (version.SourceObjectKind != SourceObjectKinds.Table)
            return $"'{version.SourceObject}' is a {version.SourceObjectKind}; generated columns can only be added to tables.";
        if (await HasColumnAsync(version.SourceObject, input.ColumnName, cancellationToken))
            return $"Table '{version.SourceObject}' already has a column named '{input.ColumnName}'.";

        var statement = MaterializedColumnDdl.AddColumn(version.SourceObject, input.ColumnName, field);
        await schemaChanges.ApplyAsync(version.SourceObject, statement, cancellationToken);
        await fields.UpdateAsync(field with { MaterializedColumn = input.ColumnName }, cancellationToken);
        return null;
    }

    /// <summary>Points a <c>json_path</c> or <c>expression</c> field at an existing column of the version's source object, or clears it.</summary>
    /// <param name="reference">The field and its draft version.</param>
    /// <param name="input">The column, or null to clear it.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>Null when the field was updated; otherwise why not: the field reads a column directly, or the source object has no such column.</returns>
    public async Task<string?> SetAsync(FieldReference reference, MaterializedColumnInput input, CancellationToken cancellationToken)
    {
        var field = await fields.GetAsync(new { reference.FieldId }, cancellationToken);
        var version = await versions.GetAsync(new { DatasetVersionId = reference.VersionId }, cancellationToken);
        if (field.FieldSourceKind == FieldSourceKinds.Column)
            return $"Field '{field.FieldKey}' reads column '{field.SourceColumn}' directly; only json_path and expression fields have a materialized column.";
        if (input.ColumnName is not null && !await HasColumnAsync(version.SourceObject, input.ColumnName, cancellationToken))
            return $"'{version.SourceObject}' has no column named '{input.ColumnName}'.";

        await fields.UpdateAsync(field with { MaterializedColumn = input.ColumnName }, cancellationToken);
        return null;
    }

    /// <summary>Whether a source object has a column; StarRocks column names ignore case.</summary>
    private async Task<bool> HasColumnAsync(string objectName, string columnName, CancellationToken cancellationToken)
    {
        var columns = await sourceObjects.ListColumnsAsync(objectName, cancellationToken);
        return columns.Any(column => string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase));
    }
}
