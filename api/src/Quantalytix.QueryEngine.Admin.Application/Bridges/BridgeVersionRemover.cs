using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Hard-deletes bridge versions, their mappings first, and bridges once no snapshot holds a version of them.</summary>
public sealed class BridgeVersionRemover(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings) : IVersionRemover
{
    /// <summary><c>bridge</c>.</summary>
    public string Kind => SnapshotKinds.Bridge;

    /// <summary>Deletes the versions' mappings, then the versions.</summary>
    /// <param name="versionIds">The bridge versions.</param>
    /// <param name="cancellationToken">Cancels the remaining statement.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveVersionsAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await mappings.DeleteAsync(new { BridgeVersionId = versionIds }, cancellationToken);
        await versions.DeleteAsync(new { BridgeVersionId = versionIds }, cancellationToken);
    }

    /// <summary>Deletes bridge rows.</summary>
    /// <param name="identityIds">The bridges.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveIdentitiesAsync(IReadOnlyCollection<long> identityIds, CancellationToken cancellationToken)
    {
        await bridges.DeleteAsync(new { BridgeId = identityIds }, cancellationToken);
    }
}
