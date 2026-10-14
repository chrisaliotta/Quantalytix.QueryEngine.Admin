using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Gives each source ID of a cloned version one new ID, so references between the cloned rows (a member's parent, a
/// band's member) point at the clones. Create one per clone operation.
/// </summary>
public sealed class IdRemap(IIdGenerator ids)
{
    /// <summary>The new ID of every source ID mapped so far.</summary>
    private readonly Dictionary<long, long> _targets = [];

    /// <summary>Returns the new ID of a source ID, allocating it on first use.</summary>
    /// <param name="sourceId">An ID of the version being cloned.</param>
    /// <returns>The same new ID for every call with the same source ID.</returns>
    public long Map(long sourceId)
    {
        if (!_targets.TryGetValue(sourceId, out var targetId))
        {
            targetId = ids.NextId();
            _targets[sourceId] = targetId;
        }

        return targetId;
    }

    /// <summary>Maps an optional reference; a missing reference stays missing.</summary>
    /// <param name="sourceId">An ID of the version being cloned, or null.</param>
    /// <returns>The new ID, or null when the source ID is null.</returns>
    public long? MapOptional(long? sourceId)
    {
        if (sourceId is null) return null;
        return Map(sourceId.Value);
    }

    /// <summary>Whether a source ID has been mapped, which tells cloned references from references to rows outside the clone.</summary>
    /// <param name="sourceId">An ID of the version being cloned.</param>
    /// <returns>True when <see cref="Map"/> has allocated a new ID for it.</returns>
    public bool Contains(long sourceId)
    {
        return _targets.ContainsKey(sourceId);
    }
}
