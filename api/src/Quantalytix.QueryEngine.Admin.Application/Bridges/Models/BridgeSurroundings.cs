using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The rows outside a bridge aggregate that its operations, rules and read models consult, read once when it loads: a change set's
/// operations apply in memory and cannot read.
/// </summary>
public sealed record BridgeSurroundings
{
    /// <summary>The dimension the bridge maps from.</summary>
    public required DimensionRecord Source { get; init; }

    /// <summary>The keys of the source dimension's live version, which source member keys resolve in; null while it has none.</summary>
    public required DimensionKeys? SourcePublished { get; init; }

    /// <summary>The dimension the bridge maps into.</summary>
    public required DimensionRecord Target { get; init; }

    /// <summary>The keys of the target dimension's live version, which target member keys resolve in; null while it has none.</summary>
    public required DimensionKeys? TargetPublished { get; init; }

    /// <summary>The report source that owns a report bridge; null for an analytical bridge.</summary>
    public required BridgeOwner? Owner { get; init; }

    /// <summary>Every version of the bridge as stored, by version number.</summary>
    public required IReadOnlyList<BridgeVersionRecord> Versions { get; init; }

    /// <summary>The mapping count of every version, by version id.</summary>
    public required IReadOnlyDictionary<long, int> MappingCounts { get; init; }

    /// <summary>The dataset bindings that remap through the bridge.</summary>
    public required IReadOnlyList<MappingBindingUsage> Bindings { get; init; }

    /// <summary>The assumption sets that pin a version.</summary>
    public required IReadOnlyList<MappingAssumptionPin> Pins { get; init; }
}
