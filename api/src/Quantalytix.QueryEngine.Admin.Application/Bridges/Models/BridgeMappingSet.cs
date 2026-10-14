using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The mappings of one bridge version as the mapping rules check them, with the member keys of its two endpoint versions,
/// which their messages name.
/// </summary>
public sealed record BridgeMappingSet
{
    /// <summary>The bridge version, which holds the cardinality and allocation mode.</summary>
    public required BridgeVersionRecord Version { get; init; }

    /// <summary>The mappings, by source member, then target member, as <see cref="Describe"/> names them.</summary>
    public required IReadOnlyList<BridgeMappingRecord> Mappings { get; init; }

    /// <summary>The key of each member of the source dimension version, by member ID.</summary>
    public required IReadOnlyDictionary<long, string> SourceMemberKeys { get; init; }

    /// <summary>The key of each member of the target dimension version, by member ID.</summary>
    public required IReadOnlyDictionary<long, string> TargetMemberKeys { get; init; }

    /// <summary>A mapping as messages name it, such as <c>'CA' -> 'WEST'</c>.</summary>
    /// <param name="mapping">One of the set's mappings.</param>
    /// <returns>The mapping's source and target members.</returns>
    public string Describe(BridgeMappingRecord mapping)
    {
        return $"{DescribeSource(mapping.SourceMemberId)} -> {DescribeTarget(mapping.TargetMemberId)}";
    }

    /// <summary>A source member as messages name it: its quoted key, or its ID when it is outside the source dimension version.</summary>
    /// <param name="memberId">A source member of the set's mappings.</param>
    /// <returns>The member's key or ID.</returns>
    public string DescribeSource(long memberId)
    {
        return Name(SourceMemberKeys, memberId);
    }

    /// <summary>A target member as messages name it: its quoted key, or its ID when it is outside the target dimension version.</summary>
    /// <param name="memberId">A target member of the set's mappings.</param>
    /// <returns>The member's key or ID.</returns>
    public string DescribeTarget(long memberId)
    {
        return Name(TargetMemberKeys, memberId);
    }

    /// <summary>The target members of some mappings as messages list them, such as <c>'EAST', 'WEST'</c>.</summary>
    /// <param name="mappings">Mappings of the set, such as those of one source member.</param>
    /// <returns>The targets, comma-separated, in the mappings' order.</returns>
    public string DescribeTargets(IEnumerable<BridgeMappingRecord> mappings)
    {
        return string.Join(", ", mappings.Select(mapping => DescribeTarget(mapping.TargetMemberId)));
    }

    /// <summary>A member's quoted key from an endpoint's keys, or its ID.</summary>
    private static string Name(IReadOnlyDictionary<long, string> keys, long memberId)
    {
        if (keys.TryGetValue(memberId, out var key)) return $"'{key}'";
        return memberId.ToString(CultureInfo.InvariantCulture);
    }
}
