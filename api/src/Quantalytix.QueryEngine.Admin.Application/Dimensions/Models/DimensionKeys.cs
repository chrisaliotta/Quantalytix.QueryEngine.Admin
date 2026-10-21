using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The level and member keys of a dimension version, which bindings, mapping entries, bridge mappings and reports name.</summary>
public sealed record DimensionKeys
{
    /// <summary>The dimension version.</summary>
    public required DimensionVersionRecord Version { get; init; }

    /// <summary>The keys of the version's levels.</summary>
    public required IReadOnlySet<string> LevelKeys { get; init; }

    /// <summary>The keys of the version's members.</summary>
    public required IReadOnlySet<string> MemberKeys { get; init; }

    /// <summary>The keys of the version's members that source values must not map to directly.</summary>
    public required IReadOnlySet<string> UnassignableMemberKeys { get; init; }

    /// <summary>The number of members without a parent.</summary>
    public required int RootCount { get; init; }

    /// <summary>The key of each member's parent, for the members that have one.</summary>
    public required IReadOnlyDictionary<string, string> ParentKeys { get; init; }

    /// <summary>The keys of a version with the given levels and members, as stored or as a write would leave them.</summary>
    /// <param name="version">The dimension version.</param>
    /// <param name="levelKeys">The keys of its levels.</param>
    /// <param name="members">Its members.</param>
    /// <returns>The version's keys.</returns>
    public static DimensionKeys Of(DimensionVersionRecord version, IEnumerable<string> levelKeys, IReadOnlyCollection<DimensionMemberRecord> members)
    {
        var keyOf = members.ToDictionary(member => member.MemberId, member => member.MemberKey);
        return new DimensionKeys
        {
            Version = version,
            LevelKeys = levelKeys.ToHashSet(StringComparer.Ordinal),
            MemberKeys = members.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal),
            UnassignableMemberKeys = members.Where(member => !member.IsAssignable).Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal),
            RootCount = members.Count(member => member.ParentMemberId is null),
            ParentKeys = members
                .Where(member => member.ParentMemberId is not null)
                .ToDictionary(member => member.MemberKey, member => keyOf[member.ParentMemberId!.Value], StringComparer.Ordinal)
        };
    }

    /// <summary>A member and every member above it, nearest first; a published version's hierarchy has no cycles.</summary>
    /// <param name="memberKey">The key of a member of the version.</param>
    /// <returns>The member's key, then its parent's, up to its root's.</returns>
    public IEnumerable<string> SelfAndAncestors(string memberKey)
    {
        for (string? key = memberKey; key is not null; key = ParentKeys.GetValueOrDefault(key))
            yield return key;
    }

    /// <summary>Whether a key names an assignable member of the version, one values are read for.</summary>
    /// <param name="memberKey">A member key.</param>
    /// <returns>True when the version has the member and it is assignable.</returns>
    public bool IsAssignable(string memberKey)
    {
        return MemberKeys.Contains(memberKey) && !UnassignableMemberKeys.Contains(memberKey);
    }

    /// <summary>
    /// Whether an assignable member sits anywhere under a member. The walk visits each member once, because a live edit of a
    /// published version can leave a parent cycle until the version is validated again.
    /// </summary>
    /// <param name="memberKey">A member key.</param>
    /// <returns>True when the version has an assignable member under the member.</returns>
    public bool HasAssignableBelow(string memberKey)
    {
        var children = ParentKeys.ToLookup(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { memberKey };
        var pending = new Queue<string>(children[memberKey]);
        while (pending.TryDequeue(out var key))
        {
            if (!visited.Add(key)) continue;
            if (IsAssignable(key)) return true;
            foreach (var child in children[key]) pending.Enqueue(child);
        }
        return false;
    }

    /// <summary>Whether the version gives a live object what it needs of the key it names.</summary>
    /// <param name="reference">A key reference.</param>
    /// <returns>True when the version satisfies the reference's requirement.</returns>
    public bool Resolves(KeyReference reference)
    {
        if (reference.IsLevel) return LevelKeys.Contains(reference.Key);
        return reference.Requirement switch
        {
            KeyRequirement.Assignable => IsAssignable(reference.Key),
            KeyRequirement.AssignableBelow => HasAssignableBelow(reference.Key),
            _ => MemberKeys.Contains(reference.Key)
        };
    }
}
