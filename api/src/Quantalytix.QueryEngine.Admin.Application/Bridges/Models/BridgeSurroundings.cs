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

    /// <summary>The keys of the source dimension's version in the bridge version's snapshot; null when it holds none.</summary>
    public required DimensionKeys? SourceKeys { get; init; }

    /// <summary>The dimension the bridge maps into.</summary>
    public required DimensionRecord Target { get; init; }

    /// <summary>The keys of the target dimension's version in the bridge version's snapshot; null when it holds none.</summary>
    public required DimensionKeys? TargetKeys { get; init; }

    /// <summary>The report source that owns a report bridge; null for an analytical bridge.</summary>
    public required BridgeOwner? Owner { get; init; }

    /// <summary>The live snapshot, which tells whether the version may change.</summary>
    public required long LiveSnapshotId { get; init; }

    /// <summary>Every version of the bridge as stored, one per snapshot.</summary>
    public required IReadOnlyList<BridgeVersionRecord> Versions { get; init; }

    /// <summary>The keys of the frozen snapshots holding a version of the bridge, which block deleting it.</summary>
    public required IReadOnlyList<string> FrozenSnapshotKeys { get; init; }

    /// <summary>The mapping count of every version, by version id.</summary>
    public required IReadOnlyDictionary<long, int> MappingCounts { get; init; }

    /// <summary>The dataset bindings of the version's snapshot that remap through the bridge.</summary>
    public required IReadOnlyList<MappingBindingUsage> Bindings { get; init; }
}
