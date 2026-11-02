using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>
/// Names the frozen snapshots among those holding an identity's versions, which block deleting the identity: a frozen snapshot is
/// read-only, so the identity goes only with every snapshot that holds it.
/// </summary>
/// <param name="snapshots">The snapshot rows.</param>
public sealed class SnapshotHolders(ITableGateway<SnapshotRecord> snapshots)
{
    /// <summary>The keys of the frozen snapshots among some snapshots.</summary>
    /// <param name="snapshotIds">The snapshots of an identity's versions.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The frozen snapshots' keys.</returns>
    public async Task<IReadOnlyList<string>> FrozenKeysAsync(IEnumerable<long> snapshotIds, CancellationToken cancellationToken)
    {
        var frozen = await snapshots.ListAsync(new { SnapshotId = snapshotIds.Distinct().ToArray(), IsLive = false }, cancellationToken);
        return [.. frozen.Select(snapshot => snapshot.SnapshotKey)];
    }
}
