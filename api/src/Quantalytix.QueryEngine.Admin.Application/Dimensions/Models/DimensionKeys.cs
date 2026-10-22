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

    /// <summary>A member and every member above it, nearest first; a published version's hierarchy has no cycles.</summary>
    /// <param name="memberKey">The key of a member of the version.</param>
    /// <returns>The member's key, then its parent's, up to its root's.</returns>
    public IEnumerable<string> SelfAndAncestors(string memberKey)
    {
        for (string? key = memberKey; key is not null; key = ParentKeys.GetValueOrDefault(key))
            yield return key;
    }

    /// <summary>Whether the version has the level or member a reference names.</summary>
    /// <param name="reference">A key reference.</param>
    /// <returns>True when the version has the key.</returns>
    public bool Has(KeyReference reference)
    {
        return reference.IsLevel ? LevelKeys.Contains(reference.Key) : MemberKeys.Contains(reference.Key);
    }
}
