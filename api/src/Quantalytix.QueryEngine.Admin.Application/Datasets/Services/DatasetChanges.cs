using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The dataset version change set (OI-01) behind <c>dataset-versions/{versionId}/changes</c>. A commit previews first, so every
/// operation and gate has passed before its DDL runs on the managed table (<see cref="DatasetSchemaChanges"/>), then writes the rows,
/// then materializes the JSON path fields whose input asked for it.
/// </summary>
public sealed class DatasetChanges(
    ChangeSetPipeline<DatasetSnapshot, DatasetOperation, DatasetVersion> pipeline,
    DatasetSchemaChanges schema,
    DatasetMaterializations materializations,
    DatasetQueries queries)
{
    /// <summary>Applies the operations in memory and writes nothing.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pending state and findings, or the refusal.</returns>
    public Task<Outcome<ChangeSetPreview<DatasetVersion>>> PreviewAsync(long versionId, ChangeSetRequest<DatasetOperation> request, CancellationToken cancellationToken)
    {
        return pipeline.PreviewAsync(versionId, request, cancellationToken);
    }

    /// <summary>Applies the operations to the version as stored now and writes the result.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancels the reads, statements and writes.</param>
    /// <returns>The stored state and id map, or the refusal with nothing written.</returns>
    public async Task<Outcome<ChangeSetCommit<DatasetVersion>>> CommitAsync(long versionId, ChangeSetRequest<DatasetOperation> request, CancellationToken cancellationToken)
    {
        var preview = await pipeline.PreviewAsync(versionId, request, cancellationToken);
        if (!preview.Succeeded) return Outcome<ChangeSetCommit<DatasetVersion>>.Refused(preview.Refusal);
        await schema.ApplyAsync(versionId, request.Operations, preview.Value.State.Settings.SourceObject, cancellationToken);
        var commit = await pipeline.CommitAsync(versionId, request, cancellationToken);
        if (!commit.Succeeded) return commit;

        var requested = request.Operations
            .OfType<DatasetSaveFieldOperation>()
            .Where(operation => operation.Input.Materialize == true && operation.Input.FieldSourceKind == FieldSourceKinds.JsonPath)
            .Select(operation => operation.Input.Key)
            .ToHashSet(StringComparer.Ordinal);
        var fields = commit.Value.State.Fields.Where(field => requested.Contains(field.Key) && field.Materialization is null).ToList();
        if (fields.Count == 0) return commit;
        foreach (var field in fields) await materializations.MaterializeAsync(versionId, long.Parse(field.Id, CultureInfo.InvariantCulture), cancellationToken);
        return Outcome<ChangeSetCommit<DatasetVersion>>.Success(commit.Value with { State = await queries.VersionAsync(versionId, cancellationToken) });
    }
}
