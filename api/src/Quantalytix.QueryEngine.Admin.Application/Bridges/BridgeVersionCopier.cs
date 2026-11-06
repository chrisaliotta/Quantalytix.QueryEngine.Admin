using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Copies a bridge version, its settings and mappings included, into a snapshot, for capture and restore.</summary>
/// <param name="versions">Reads the source and writes the copy's row.</param>
/// <param name="mappings">Clears the mappings a copy replaces.</param>
/// <param name="store">Copies the mappings.</param>
/// <param name="ids">Allocates a new version's id.</param>
/// <param name="time">Stamps the copy.</param>
public sealed class BridgeVersionCopier(
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    BridgeStore store,
    IIdGenerator ids,
    TimeProvider time) : IVersionCopier
{
    /// <summary><c>bridge</c>.</summary>
    public string Kind => SnapshotKinds.Bridge;

    /// <summary>Copies versions into a snapshot, each as a new version or over another version of its bridge.</summary>
    /// <param name="copies">The versions to copy, each with the version whose settings and mappings it replaces, if any.</param>
    /// <param name="targetSnapshotId">The snapshot the copies belong to.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copies' ids.</returns>
    public async Task<IReadOnlyList<long>> CopyAsync(IReadOnlyList<VersionCopy> copies, long targetSnapshotId, CancellationToken cancellationToken)
    {
        var batch = new VersionCopyBatch(copies, ids);
        var now = time.GetUtcNow().UtcDateTime;
        var sources = await versions.ListAsync(new { BridgeVersionId = batch.SourceIds }, cancellationToken);
        await mappings.DeleteAsync(new { BridgeVersionId = batch.ReplacedIds }, cancellationToken);
        await versions.InsertManyAsync(
            [.. sources.Select(source => source with { BridgeVersionId = batch.Targets[source.BridgeVersionId], SnapshotId = targetSnapshotId, CreatedAt = now })],
            cancellationToken);
        await store.CopyMappingsAsync(batch.Targets, cancellationToken);
        return batch.CopyIds;
    }

    /// <summary>The version's settings and its mappings, each by member keys with its factor, weight and selection.</summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content key.</returns>
    public async Task<string> ContentKeyAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { BridgeVersionId = versionId }, cancellationToken);
        var rows = await mappings.ListAsync(new { BridgeVersionId = versionId }, cancellationToken);
        return ContentKey.Of(
            [version with { BridgeVersionId = 0, SnapshotId = 0, CreatedAt = default }],
            rows.Select(mapping => mapping with { BridgeMappingId = 0, BridgeVersionId = 0 }));
    }
}
