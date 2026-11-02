using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Hard-deletes bridge versions, their mappings first, and a bridge once no snapshot holds a version of it.</summary>
public sealed class BridgeVersionRemover(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings) : IVersionRemover
{
    /// <summary><c>bridge</c>.</summary>
    public string Kind => SnapshotKinds.Bridge;

    /// <summary>Deletes a version's mappings, then the version.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the remaining statement.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        await mappings.DeleteAsync(new { BridgeVersionId = versionId }, cancellationToken);
        await versions.DeleteAsync(new { BridgeVersionId = versionId }, cancellationToken);
    }

    /// <summary>Deletes a bridge row.</summary>
    /// <param name="identityId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the row is gone.</returns>
    public async Task RemoveIdentityAsync(long identityId, CancellationToken cancellationToken)
    {
        await bridges.DeleteAsync(new { BridgeId = identityId }, cancellationToken);
    }
}
