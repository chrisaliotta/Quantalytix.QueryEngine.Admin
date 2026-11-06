using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>
/// The snapshot commands: capture the live snapshot into a frozen one, restore a frozen one into live, relabel and delete a frozen
/// one. The live snapshot is never relabelled, deleted or restored. Capture, restore and delete change what requests can name or
/// read, so they advance the semantic revision; relabelling does not.
/// </summary>
public sealed class SnapshotCommands(
    ITableGateway<SnapshotRecord> snapshots,
    ITableGateway<SnapshotRestoreRecord> restores,
    ITableGateway<DatabaseInfoRecord> databaseInfo,
    SnapshotIndex index,
    LiveSnapshot live,
    IEnumerable<IVersionCopier> copiers,
    IEnumerable<IVersionRemover> removers,
    IEnumerable<IVersionRules> rules,
    SemanticRevision semanticRevision,
    TenantInfoReader tenant,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>
    /// Copies every live version into a new frozen snapshot, then writes the snapshot row last, so a stop leaves copies no request can
    /// name rather than a snapshot missing versions. The semantic revision is read before and after the copies: a commit in between
    /// may have been copied half, so the copies are deleted and the capture is refused.
    /// </summary>
    /// <param name="input">The validated key and labels.</param>
    /// <param name="capturedBy">The token subject of the caller.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new snapshot, or a conflict when the key is taken or live changed during the capture.</returns>
    public async Task<Outcome<Snapshot>> CaptureAsync(CaptureSnapshotInput input, string capturedBy, CancellationToken cancellationToken)
    {
        if ((await snapshots.ListAsync(new { SnapshotKey = input.Key }, cancellationToken)).Count > 0)
            return Outcome<Snapshot>.Refused(Refusal.Conflict("Key taken", $"Another snapshot already uses the key '{input.Key}'."));

        var revision = await RevisionAsync(cancellationToken);
        var snapshotId = ids.NextId();
        var copies = new List<(string Kind, IReadOnlyList<long> VersionIds)>();
        foreach (var kind in SnapshotIndex.Of(await index.ReadAsync(cancellationToken), await live.IdAsync(cancellationToken)).GroupBy(entry => entry.Kind))
            copies.Add((kind.Key, await Copier(kind.Key).CopyAsync([.. kind.Select(entry => new VersionCopy(entry.VersionId, null))], snapshotId, cancellationToken)));

        if (await RevisionAsync(cancellationToken) != revision)
        {
            foreach (var (kind, versionIds) in copies.AsEnumerable().Reverse()) await Remover(kind).RemoveVersionsAsync(versionIds, cancellationToken);
            return Outcome<Snapshot>.Refused(Refusal.Conflict("Live changed during the capture", "Metadata was saved while the snapshot was captured; nothing was captured, so capture again."));
        }

        var now = time.GetUtcNow().UtcDateTime;
        var row = new SnapshotRecord
        {
            SnapshotId = snapshotId,
            SnapshotKey = input.Key,
            DisplayName = input.DisplayName.Trim(),
            Description = input.Description,
            IsLive = false,
            CapturedAt = now,
            CapturedBy = capturedBy,
            CreatedAt = now
        };
        await snapshots.InsertAsync(row, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<Snapshot>.Success(new Snapshot(row));
    }

    /// <summary>
    /// Makes live equal a frozen snapshot. Every version the snapshot holds must pass its kind's rules first, in the snapshot, as live
    /// would hold them; any violation refuses the restore and nothing changes. Then each frozen version is copied over its identity's
    /// live version, or into live when live lacks the identity; the live version of an identity the snapshot lacks is deleted, and the
    /// identity with it when no frozen snapshot holds it. The restore is logged.
    /// </summary>
    /// <param name="snapshotId">The frozen snapshot.</param>
    /// <param name="restoredBy">The token subject of the caller.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The tenant state the restore produced, a conflict for the live snapshot, or the rules the snapshot breaks.</returns>
    public async Task<Outcome<TenantInfo>> RestoreAsync(long snapshotId, string restoredBy, CancellationToken cancellationToken)
    {
        var row = await snapshots.GetAsync(new { SnapshotId = snapshotId }, cancellationToken);
        if (row.IsLive) return Outcome<TenantInfo>.Refused(LiveSnapshotRefusal(row, "restored"));

        var holdings = await index.ReadAsync(cancellationToken);
        var frozen = SnapshotIndex.Of(holdings, snapshotId);
        var violations = new List<RuleViolation>();
        foreach (var entry in frozen) violations.AddRange(await rules.Single(rule => rule.Kind == entry.Kind).EvaluateAsync(entry.VersionId, cancellationToken));
        if (violations.Count > 0)
            return Outcome<TenantInfo>.Refused(Refusal.RulesViolated($"Snapshot '{row.SnapshotKey}' breaks rules against the database as it is now; nothing was restored.", violations));

        var liveSnapshotId = await live.IdAsync(cancellationToken);
        var current = SnapshotIndex.Of(holdings, liveSnapshotId).ToDictionary(entry => (entry.Kind, entry.IdentityId));
        var restored = frozen.Select(entry => (entry.Kind, entry.IdentityId)).ToHashSet();
        foreach (var kind in frozen.GroupBy(entry => entry.Kind))
        {
            VersionCopy[] copies = [.. kind.Select(entry => new VersionCopy(entry.VersionId, current.GetValueOrDefault((entry.Kind, entry.IdentityId))?.VersionId))];
            await Copier(kind.Key).CopyAsync(copies, liveSnapshotId, cancellationToken);
        }

        await RemoveAsync(current.Values.Where(entry => !restored.Contains((entry.Kind, entry.IdentityId))), HeldOutside(holdings, liveSnapshotId), cancellationToken);

        await restores.InsertAsync(
            new SnapshotRestoreRecord { SnapshotRestoreId = ids.NextId(), SnapshotId = snapshotId, RestoredAt = time.GetUtcNow().UtcDateTime, RestoredBy = restoredBy },
            cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<TenantInfo>.Success(await tenant.ReadAsync(cancellationToken));
    }

    /// <summary>Changes a frozen snapshot's display name and description; its key and versions stay.</summary>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="input">The validated labels.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The relabelled snapshot, or a conflict for the live snapshot.</returns>
    public async Task<Outcome<Snapshot>> RelabelAsync(long snapshotId, RelabelSnapshotInput input, CancellationToken cancellationToken)
    {
        var row = await snapshots.GetAsync(new { SnapshotId = snapshotId }, cancellationToken);
        if (row.IsLive) return Outcome<Snapshot>.Refused(LiveSnapshotRefusal(row, "relabelled"));
        var relabelled = row with { DisplayName = input.DisplayName.Trim(), Description = input.Description };
        await snapshots.UpdateAsync(relabelled, cancellationToken);
        return Outcome<Snapshot>.Success(new Snapshot(relabelled));
    }

    /// <summary>
    /// Deletes a frozen snapshot with every version it holds and their content, and each identity no other snapshot holds. The snapshot
    /// row goes first, so a stop leaves versions no request can name rather than a snapshot missing versions.
    /// </summary>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="cancellationToken">Cancels the read and the deletes.</param>
    /// <returns>Null when deleted; a conflict for the live snapshot.</returns>
    public async Task<Refusal?> DeleteAsync(long snapshotId, CancellationToken cancellationToken)
    {
        var row = await snapshots.GetAsync(new { SnapshotId = snapshotId }, cancellationToken);
        if (row.IsLive) return LiveSnapshotRefusal(row, "deleted");

        var holdings = await index.ReadAsync(cancellationToken);
        await snapshots.DeleteAsync(new { SnapshotId = snapshotId }, cancellationToken);
        await restores.DeleteAsync(new { SnapshotId = snapshotId }, cancellationToken);
        await RemoveAsync(SnapshotIndex.Of(holdings, snapshotId), HeldOutside(holdings, snapshotId), cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }

    /// <summary>
    /// Removes the entries' versions, kind by kind in reverse capture order with one batch per kind, and each entry's identity when no
    /// other snapshot holds it.
    /// </summary>
    private async Task RemoveAsync(IEnumerable<SnapshotEntry> entries, IReadOnlySet<(string Kind, long IdentityId)> heldElsewhere, CancellationToken cancellationToken)
    {
        foreach (var kind in entries.Reverse().GroupBy(entry => entry.Kind))
        {
            var remover = Remover(kind.Key);
            await remover.RemoveVersionsAsync([.. kind.Select(entry => entry.VersionId)], cancellationToken);
            await remover.RemoveIdentitiesAsync([.. kind.Where(entry => !heldElsewhere.Contains((entry.Kind, entry.IdentityId))).Select(entry => entry.IdentityId)], cancellationToken);
        }
    }

    /// <summary>The identities some snapshot other than the given one holds a version of.</summary>
    private static HashSet<(string Kind, long IdentityId)> HeldOutside(IEnumerable<SnapshotHolding> holdings, long snapshotId)
    {
        return holdings.Where(holding => holding.SnapshotId != snapshotId).Select(holding => (holding.Entry.Kind, holding.Entry.IdentityId)).ToHashSet();
    }

    /// <summary>The tenant's semantic revision as stored now.</summary>
    private async Task<long> RevisionAsync(CancellationToken cancellationToken)
    {
        return (await databaseInfo.GetAsync(new { SingletonId }, cancellationToken)).SemanticRevision;
    }

    /// <summary>The copier of a kind.</summary>
    private IVersionCopier Copier(string kind)
    {
        return copiers.Single(copier => copier.Kind == kind);
    }

    /// <summary>The remover of a kind.</summary>
    private IVersionRemover Remover(string kind)
    {
        return removers.Single(remover => remover.Kind == kind);
    }

    /// <summary>The conflict of a command the live snapshot refuses.</summary>
    private static Refusal LiveSnapshotRefusal(SnapshotRecord row, string done)
    {
        return Refusal.Conflict("The live snapshot", $"Snapshot '{row.SnapshotKey}' is the live snapshot, which every edit writes; it cannot be {done}.");
    }
}
