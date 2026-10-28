using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// A draft release's manifest as a change-set aggregate (OI-01): its one operation, <c>saveManifest</c>, replaces the manifest. The
/// engine reads only the active release, which is never a draft, so a commit leaves the semantic revision alone.
/// </summary>
/// <param name="loader">Reads the release as stored.</param>
/// <param name="manifest">The manifest table.</param>
public sealed class ReleaseAggregate(ReleaseSnapshotLoader loader, ITableGateway<ReleaseDatasetRecord> manifest)
    : IChangeSetAggregate<ReleaseSnapshot, ReleaseOperation, ReleaseDetail>
{
    /// <summary>Reads the release as stored.</summary>
    /// <param name="aggregateId">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public Task<ReleaseSnapshot> LoadAsync(long aggregateId, CancellationToken cancellationToken)
    {
        return loader.LoadAsync(aggregateId, cancellationToken);
    }

    /// <summary>Replaces the manifest; a dataset that no longer exists is a conflict, because another author deleted it.</summary>
    /// <param name="snapshot">The release as the earlier operations left it.</param>
    /// <param name="operation">A <see cref="ReleaseSaveManifestOperation"/>.</param>
    /// <param name="context">Takes the refusal.</param>
    /// <returns>The release with the new manifest, by dataset.</returns>
    public ReleaseSnapshot Apply(ReleaseSnapshot snapshot, ReleaseOperation operation, ChangeContext context)
    {
        var save = (ReleaseSaveManifestOperation)operation;
        var datasets = snapshot.Surroundings.Datasets.Select(dataset => dataset.DatasetId).ToHashSet();
        if (save.Manifest.FirstOrDefault(entry => !datasets.Contains(entry.DatasetId)) is { } missing)
        {
            context.Conflict("Dataset not found", $"Dataset {missing.DatasetId} does not exist; another author may have deleted it.");
            return snapshot;
        }

        return snapshot with
        {
            Manifest =
            [
                .. save.Manifest
                    .OrderBy(entry => entry.DatasetId)
                    .Select(entry => new ReleaseDatasetRecord
                    {
                        SemanticReleaseId = snapshot.Release.SemanticReleaseId,
                        DatasetId = entry.DatasetId,
                        DatasetVersionId = entry.DatasetVersionId
                    })
            ]
        };
    }

    /// <summary>Deletes the rows of datasets no longer pinned, updates repinned rows and inserts new ones.</summary>
    /// <param name="before">The release as stored.</param>
    /// <param name="after">The release with the new manifest.</param>
    /// <param name="plan">The commit's writes.</param>
    public void Plan(ReleaseSnapshot before, ReleaseSnapshot after, WritePlan plan)
    {
        plan.Sync(manifest, 1, before.Manifest, after.Manifest, row => new { row.SemanticReleaseId, row.DatasetId });
    }

    /// <summary>A draft's manifest never reaches queries.</summary>
    /// <param name="before">Unused.</param>
    /// <param name="after">Unused.</param>
    /// <returns>False.</returns>
    public bool IsEngineVisible(ReleaseSnapshot before, ReleaseSnapshot after)
    {
        return false;
    }

    /// <summary>The release detail; every id in it is stored, so <paramref name="presentId"/> is not needed.</summary>
    /// <param name="snapshot">The release.</param>
    /// <param name="presentId">Unused.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>The detail.</returns>
    public Task<ReleaseDetail> ReadAsync(ReleaseSnapshot snapshot, Func<long, string> presentId, CancellationToken cancellationToken)
    {
        return Task.FromResult(ReleaseReadModels.Detail(snapshot));
    }
}
