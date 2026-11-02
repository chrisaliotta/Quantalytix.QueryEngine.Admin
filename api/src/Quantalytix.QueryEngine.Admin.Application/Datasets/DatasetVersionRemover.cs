using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Deletes dataset versions with their content, and a dataset identity no snapshot holds. Managed tables stay: their registry
/// rows name the dataset that created them, and only deleting a dataset through its own command drops them.
/// </summary>
/// <param name="datasets">The dataset identities.</param>
/// <param name="versions">The dataset versions.</param>
/// <param name="content">Deletes a version's content.</param>
public sealed class DatasetVersionRemover(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    DatasetVersionContentStore content) : IVersionRemover
{
    /// <summary><c>dataset</c>.</summary>
    public string Kind => SnapshotKinds.Dataset;

    /// <summary>Deletes a version's content, then the version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        await content.DeleteAsync([versionId], cancellationToken);
        await versions.DeleteAsync(new { DatasetVersionId = versionId }, cancellationToken);
    }

    /// <summary>Deletes a dataset row.</summary>
    /// <param name="identityId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the row is gone.</returns>
    public async Task RemoveIdentityAsync(long identityId, CancellationToken cancellationToken)
    {
        await datasets.DeleteAsync(new { DatasetId = identityId }, cancellationToken);
    }
}
