using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>
/// The tenant's live snapshot, read once per request: the snapshot whose versions are edited in place and which every aggregate
/// loader resolves an identity's version in.
/// </summary>
/// <param name="snapshots">The snapshot rows.</param>
public sealed class LiveSnapshot(ITableGateway<SnapshotRecord> snapshots)
{
    /// <summary>The live snapshot row once read.</summary>
    private SnapshotRecord? _live;

    /// <summary>The live snapshot's id.</summary>
    /// <param name="cancellationToken">Cancels the first read.</param>
    /// <returns>The id.</returns>
    public async Task<long> IdAsync(CancellationToken cancellationToken)
    {
        return (await ReadAsync(cancellationToken)).SnapshotId;
    }

    /// <summary>The live snapshot row.</summary>
    /// <param name="cancellationToken">Cancels the first read.</param>
    /// <returns>The row.</returns>
    public async Task<SnapshotRecord> ReadAsync(CancellationToken cancellationToken)
    {
        return _live ??= (await snapshots.ListAsync(new { IsLive = true }, cancellationToken)).Single();
    }
}
