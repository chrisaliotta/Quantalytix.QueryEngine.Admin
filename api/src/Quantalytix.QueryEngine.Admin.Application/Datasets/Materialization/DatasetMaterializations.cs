using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The materialization commands of JSON path fields (OI-32). A source object has one asynchronous materialized view holding the
/// columns its fields name in <c>qe_field.materialized_column</c>; every change of that set redefines the view, which StarRocks then
/// refreshes, and the last column out drops it. Storage does not change what a version means, so any version that is not retired
/// accepts these commands; the engine reads <c>materialized_column</c>, so each change advances the semantic revision.
/// </summary>
public sealed class DatasetMaterializations(
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<FieldRecord> fields,
    IMaterializedViews views,
    SemanticRevision semanticRevision)
{
    /// <summary>Adds a JSON path field's column to its source object's view.</summary>
    /// <param name="versionId">The field's version.</param>
    /// <param name="fieldId">The field.</param>
    /// <param name="cancellationToken">Cancels the reads and statements.</param>
    /// <returns>The field with its materialization, or the conflict of a retired version or another field kind.</returns>
    public async Task<Outcome<DatasetField>> MaterializeAsync(long versionId, long fieldId, CancellationToken cancellationToken)
    {
        var (version, field, refusal) = await TargetAsync(versionId, fieldId, cancellationToken);
        if (refusal is not null) return Outcome<DatasetField>.Refused(refusal);
        var materialized = field with { MaterializedColumn = MaterializedViewNames.ColumnOf(field) };
        await fields.UpdateAsync(materialized, cancellationToken);
        await DefineAsync(version.SourceObject, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<DatasetField>.Success(await ReadAsync(version, materialized, cancellationToken));
    }

    /// <summary>Starts a refresh of a materialized field's view.</summary>
    /// <param name="versionId">The field's version.</param>
    /// <param name="fieldId">The field.</param>
    /// <param name="cancellationToken">Cancels the reads and the statement.</param>
    /// <returns>The field, or the conflict of a retired version or a field that is not materialized.</returns>
    public async Task<Outcome<DatasetField>> RefreshAsync(long versionId, long fieldId, CancellationToken cancellationToken)
    {
        var (version, field, refusal) = await TargetAsync(versionId, fieldId, cancellationToken);
        if (refusal is not null) return Outcome<DatasetField>.Refused(refusal);
        if (field.MaterializedColumn is null)
            return Outcome<DatasetField>.Refused(Refusal.Conflict("Field is not materialized", $"Field '{field.FieldKey}' has no materialized column; materialize it first."));
        await views.RefreshAsync(MaterializedViewNames.ViewOf(version.SourceObject), cancellationToken);
        return Outcome<DatasetField>.Success(await ReadAsync(version, field, cancellationToken));
    }

    /// <summary>Removes a field's column from its source object's view; the field reads its JSON path again.</summary>
    /// <param name="versionId">The field's version.</param>
    /// <param name="fieldId">The field.</param>
    /// <param name="cancellationToken">Cancels the reads and statements.</param>
    /// <returns>The field without materialization, or the conflict of a retired version or another field kind.</returns>
    public async Task<Outcome<DatasetField>> DematerializeAsync(long versionId, long fieldId, CancellationToken cancellationToken)
    {
        var (version, field, refusal) = await TargetAsync(versionId, fieldId, cancellationToken);
        if (refusal is not null) return Outcome<DatasetField>.Refused(refusal);
        var plain = field with { MaterializedColumn = null };
        await fields.UpdateAsync(plain, cancellationToken);
        await DefineAsync(version.SourceObject, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<DatasetField>.Success(DatasetReadModels.Field(plain, version.SourceObject, null, TemporaryIds.PresentStored));
    }

    /// <summary>
    /// Defines a source object's view again from the columns its versions' fields name, so a view that reads every column of the
    /// object picks up columns added since; drops the view when no field names a column.
    /// </summary>
    /// <param name="sourceObject">The table or view.</param>
    /// <param name="cancellationToken">Cancels the reads and statements.</param>
    /// <returns>A task that completes when the view is defined or dropped.</returns>
    public async Task DefineAsync(string sourceObject, CancellationToken cancellationToken)
    {
        var readers = (await versions.ListAsync(new { SourceObject = sourceObject }, cancellationToken)).Where(version => version.Status != PublicationStatuses.Retired);
        var materialized = (await fields.ListAsync(new { DatasetVersionId = readers.Select(version => version.DatasetVersionId).ToArray() }, cancellationToken))
            .Where(field => field.MaterializedColumn is not null)
            .DistinctBy(field => field.MaterializedColumn, StringComparer.Ordinal)
            .OrderBy(field => field.MaterializedColumn, StringComparer.Ordinal)
            .ToList();
        var viewName = MaterializedViewNames.ViewOf(sourceObject);
        if (materialized.Count == 0)
        {
            if ((await views.ListAsync(cancellationToken)).ContainsKey(viewName)) await views.DropAsync(viewName, cancellationToken);
            return;
        }

        await views.DefineAsync(
            new MaterializedViewDefinition
            {
                ViewName = viewName,
                SourceObject = sourceObject,
                Columns =
                [
                    .. materialized.Select(field => new MaterializedViewColumn
                    {
                        Name = field.MaterializedColumn!,
                        SourceColumn = field.SourceColumn!,
                        JsonPath = field.JsonPath!,
                        DataType = field.DataType,
                        NumericPrecision = field.NumericPrecision,
                        NumericScale = field.NumericScale
                    })
                ]
            },
            cancellationToken);
    }

    /// <summary>The version and JSON path field a command names, or the conflict that refuses it.</summary>
    private async Task<(DatasetVersionRecord Version, FieldRecord Field, Refusal? Refusal)> TargetAsync(long versionId, long fieldId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var field = await fields.GetAsync(new { FieldId = fieldId }, cancellationToken);
        if (version.Status == PublicationStatuses.Retired)
            return (version, field, Refusal.Conflict("Version is retired", $"Version v{version.VersionNumber} is retired; queries do not read it, so there is nothing to store."));
        if (field.FieldSourceKind != FieldSourceKinds.JsonPath)
            return (version, field, Refusal.Conflict("Not a JSON path field", $"Field '{field.FieldKey}' is a {field.FieldSourceKind} field; only JSON path fields are materialized."));
        return (version, field, null);
    }

    /// <summary>A field with its view's status.</summary>
    private async Task<DatasetField> ReadAsync(DatasetVersionRecord version, FieldRecord field, CancellationToken cancellationToken)
    {
        var status = (await views.ListAsync(cancellationToken)).GetValueOrDefault(MaterializedViewNames.ViewOf(version.SourceObject));
        return DatasetReadModels.Field(field, version.SourceObject, status, TemporaryIds.PresentStored);
    }
}
