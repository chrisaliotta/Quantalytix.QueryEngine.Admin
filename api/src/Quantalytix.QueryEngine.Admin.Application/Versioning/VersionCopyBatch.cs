using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// The ids of one batch of version copies: each copy keeps the id of the version it replaces or gets a new one. A copier writes
/// every row of a table for the whole batch in one statement, because each StarRocks INSERT is a load transaction of its own.
/// </summary>
public sealed class VersionCopyBatch
{
    /// <summary>Allocates the copy ids of a batch.</summary>
    /// <param name="copies">The versions to copy, each version once.</param>
    /// <param name="ids">Allocates the ids of new versions.</param>
    public VersionCopyBatch(IReadOnlyList<VersionCopy> copies, IIdGenerator ids)
    {
        SourceIds = [.. copies.Select(copy => copy.VersionId)];
        ReplacedIds = [.. copies.Where(copy => copy.TargetVersionId is not null).Select(copy => copy.TargetVersionId!.Value)];
        Targets = copies.ToDictionary(copy => copy.VersionId, copy => copy.TargetVersionId ?? ids.NextId());
    }

    /// <summary>The versions copied, in request order.</summary>
    public IReadOnlyList<long> SourceIds { get; }

    /// <summary>The versions whose content the copies replace; the copier clears it before writing.</summary>
    public IReadOnlyList<long> ReplacedIds { get; }

    /// <summary>The copy id of each copied version.</summary>
    public IReadOnlyDictionary<long, long> Targets { get; }

    /// <summary>The copy ids, in request order.</summary>
    public IReadOnlyList<long> CopyIds => [.. SourceIds.Select(id => Targets[id])];
}
