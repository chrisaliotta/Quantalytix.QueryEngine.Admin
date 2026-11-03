using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>
/// The snapshot reads: the live snapshot and the frozen ones with their version counts, a snapshot with the version it holds of each
/// identity, the comparison of two snapshots, and the counts a capture would copy. The comparison asks each kind's copier for the
/// content keys of both versions, so it flags only versions whose content differs.
/// </summary>
public sealed class SnapshotQueries(
    ITableGateway<SnapshotRecord> snapshots,
    ITableGateway<SnapshotRestoreRecord> restores,
    SnapshotIndex index,
    LiveSnapshot live,
    IEnumerable<IVersionCopier> copiers)
{
    /// <summary>The live snapshot and every frozen one, newest capture first, with their counts and latest restore.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list.</returns>
    public async Task<SnapshotList> ListAsync(CancellationToken cancellationToken)
    {
        var holdings = await index.ReadAsync(cancellationToken);
        var lastRestore = (await restores.ListAsync(null, cancellationToken))
            .GroupBy(row => row.SnapshotId)
            .ToDictionary(group => group.Key, group => new SnapshotRestore(group.MaxBy(row => row.RestoredAt)!));
        var items = (await snapshots.ListAsync(null, cancellationToken))
            .Select(row => new SnapshotListItem(row, SnapshotCounts.Of(SnapshotIndex.Of(holdings, row.SnapshotId)), lastRestore.GetValueOrDefault(row.SnapshotId)))
            .ToList();
        return new SnapshotList
        {
            Live = items.Single(item => item.IsLive),
            Frozen = [.. items.Where(item => !item.IsLive).OrderByDescending(item => item.CapturedAt)]
        };
    }

    /// <summary>A snapshot with the version it holds of each identity and its restores.</summary>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The detail.</returns>
    public async Task<SnapshotDetail> GetAsync(long snapshotId, CancellationToken cancellationToken)
    {
        return new SnapshotDetail
        {
            Snapshot = new Snapshot(await snapshots.GetAsync(new { SnapshotId = snapshotId }, cancellationToken)),
            Entries = SnapshotIndex.Of(await index.ReadAsync(cancellationToken), snapshotId),
            Restores = [.. (await restores.ListAsync(new { SnapshotId = snapshotId }, cancellationToken)).OrderByDescending(row => row.RestoredAt).Select(row => new SnapshotRestore(row))]
        };
    }

    /// <summary>How many versions of each kind a capture would copy now: every live version.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The counts.</returns>
    public async Task<SnapshotCounts> CapturePreviewAsync(CancellationToken cancellationToken)
    {
        return SnapshotCounts.Of(SnapshotIndex.Of(await index.ReadAsync(cancellationToken), await live.IdAsync(cancellationToken)));
    }

    /// <summary>
    /// What one snapshot holds that another does not, and the identities both hold with each one's version and whether their content
    /// differs.
    /// </summary>
    /// <param name="fromSnapshotId">The snapshot compared from.</param>
    /// <param name="toSnapshotId">The snapshot compared to.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The comparison, or null when either names no snapshot.</returns>
    public async Task<SnapshotComparison?> CompareAsync(long fromSnapshotId, long toSnapshotId, CancellationToken cancellationToken)
    {
        var compared = (await snapshots.ListAsync(new { SnapshotId = new[] { fromSnapshotId, toSnapshotId } }, cancellationToken)).Select(row => row.SnapshotId).ToHashSet();
        if (!compared.Contains(fromSnapshotId) || !compared.Contains(toSnapshotId)) return null;
        var holdings = await index.ReadAsync(cancellationToken);
        var before = SnapshotIndex.Of(holdings, fromSnapshotId).ToDictionary(entry => (entry.Kind, entry.IdentityId));
        var after = SnapshotIndex.Of(holdings, toSnapshotId).ToDictionary(entry => (entry.Kind, entry.IdentityId));
        var common = new List<SnapshotPair>();
        foreach (var (key, entry) in after.Where(pair => before.ContainsKey(pair.Key)))
        {
            var copier = copiers.Single(candidate => candidate.Kind == entry.Kind);
            var fromVersionId = before[key].VersionId;
            common.Add(new SnapshotPair
            {
                Kind = entry.Kind,
                IdentityId = entry.IdentityId,
                Key = entry.Key,
                FromVersionId = fromVersionId,
                ToVersionId = entry.VersionId,
                Differs = await copier.ContentKeyAsync(fromVersionId, cancellationToken) != await copier.ContentKeyAsync(entry.VersionId, cancellationToken)
            });
        }

        return new SnapshotComparison
        {
            FromSnapshotId = fromSnapshotId,
            ToSnapshotId = toSnapshotId,
            Added = [.. after.Where(pair => !before.ContainsKey(pair.Key)).Select(pair => pair.Value)],
            Removed = [.. before.Where(pair => !after.ContainsKey(pair.Key)).Select(pair => pair.Value)],
            Common = common
        };
    }
}
