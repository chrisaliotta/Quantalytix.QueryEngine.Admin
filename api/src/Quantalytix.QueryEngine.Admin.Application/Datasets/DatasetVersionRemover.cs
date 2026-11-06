using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Deletes dataset versions with their content, and dataset identities no snapshot holds. Managed tables stay: their registry
/// rows name the dataset that created them, and only deleting a dataset through its own command drops them.
/// </summary>
/// <param name="datasets">The dataset identities.</param>
/// <param name="versions">The dataset versions.</param>
/// <param name="content">Deletes the versions' content.</param>
public sealed class DatasetVersionRemover(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    DatasetVersionContentStore content) : IVersionRemover
{
    /// <summary><c>dataset</c>.</summary>
    public string Kind => SnapshotKinds.Dataset;

    /// <summary>Deletes the versions' content, then the versions.</summary>
    /// <param name="versionIds">The dataset versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveVersionsAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await content.DeleteAsync(versionIds, cancellationToken);
        await versions.DeleteAsync(new { DatasetVersionId = versionIds }, cancellationToken);
    }

    /// <summary>Deletes dataset rows.</summary>
    /// <param name="identityIds">The datasets.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveIdentitiesAsync(IReadOnlyCollection<long> identityIds, CancellationToken cancellationToken)
    {
        await datasets.DeleteAsync(new { DatasetId = identityIds }, cancellationToken);
    }
}
