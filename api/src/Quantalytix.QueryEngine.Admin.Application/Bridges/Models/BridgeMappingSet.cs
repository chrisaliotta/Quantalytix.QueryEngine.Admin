using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The mappings of one bridge version as the mapping rules check them, with the keys of the published versions of the bridge's
/// two dimensions, which the mappings' member keys resolve in.
/// </summary>
public sealed record BridgeMappingSet
{
    /// <summary>The bridge version, which holds the cardinality and allocation mode.</summary>
    public required BridgeVersionRecord Version { get; init; }

    /// <summary>The mappings, by source member key, then target member key.</summary>
    public required IReadOnlyList<BridgeMappingRecord> Mappings { get; init; }

    /// <summary>The keys of the published version of the bridge's source dimension; null while it has none.</summary>
    public required DimensionKeys? Source { get; init; }

    /// <summary>The keys of the published version of the bridge's target dimension; null while it has none.</summary>
    public required DimensionKeys? Target { get; init; }

    /// <summary>A mapping as messages name it, such as <c>'CA' -> 'WEST'</c>.</summary>
    /// <param name="mapping">One of the set's mappings.</param>
    /// <returns>The mapping's source and target members.</returns>
    public static string Describe(BridgeMappingRecord mapping)
    {
        return $"'{mapping.SourceMemberKey}' -> '{mapping.TargetMemberKey}'";
    }

    /// <summary>The target members of some mappings as messages list them, such as <c>'EAST', 'WEST'</c>.</summary>
    /// <param name="mappings">Mappings of the set, such as those of one source member.</param>
    /// <returns>The targets, comma-separated, in the mappings' order.</returns>
    public static string DescribeTargets(IEnumerable<BridgeMappingRecord> mappings)
    {
        return string.Join(", ", mappings.Select(mapping => $"'{mapping.TargetMemberKey}'"));
    }
}
