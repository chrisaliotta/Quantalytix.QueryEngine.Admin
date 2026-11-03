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

    /// <summary>Copies a version into a snapshot, as a new version or over another version of the bridge.</summary>
    /// <param name="versionId">The version to copy.</param>
    /// <param name="targetSnapshotId">The snapshot the copy belongs to.</param>
    /// <param name="targetVersionId">The version whose settings and mappings the copy replaces, or null for a new version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copy's id.</returns>
    public async Task<long> CopyAsync(long versionId, long targetSnapshotId, long? targetVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(new { BridgeVersionId = versionId }, cancellationToken);
        var copy = source with { BridgeVersionId = targetVersionId ?? ids.NextId(), SnapshotId = targetSnapshotId, CreatedAt = time.GetUtcNow().UtcDateTime };
        if (targetVersionId is { } target)
        {
            await mappings.DeleteAsync(new { BridgeVersionId = target }, cancellationToken);
            await versions.UpdateAsync(copy, cancellationToken);
        }
        else
        {
            await versions.InsertAsync(copy, cancellationToken);
        }

        await store.CopyMappingsAsync(versionId, copy.BridgeVersionId, cancellationToken);
        return copy.BridgeVersionId;
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
