using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Reads a release aggregate as stored, with the tenant's datasets and versions, the active release and its activation log.</summary>
public sealed class ReleaseSnapshotLoader(
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<ReleaseActivationRecord> activations,
    TenantInfoReader tenant)
{
    /// <summary>Reads a release with its surroundings.</summary>
    /// <param name="releaseId">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<ReleaseSnapshot> LoadAsync(long releaseId, CancellationToken cancellationToken)
    {
        var filter = new { SemanticReleaseId = releaseId };
        return new ReleaseSnapshot
        {
            Release = await releases.GetAsync(filter, cancellationToken),
            Manifest = [.. (await manifest.ListAsync(filter, cancellationToken)).OrderBy(row => row.DatasetId)],
            Surroundings = new ReleaseSurroundings
            {
                Datasets = await datasets.ListAsync(null, cancellationToken),
                DatasetVersions = await datasetVersions.ListAsync(null, cancellationToken),
                ActiveReleaseId = (await tenant.ReadAsync(cancellationToken)).ActiveReleaseId,
                Activations = [.. (await activations.ListAsync(filter, cancellationToken)).OrderByDescending(row => row.ActivatedAt)]
            }
        };
    }
}
