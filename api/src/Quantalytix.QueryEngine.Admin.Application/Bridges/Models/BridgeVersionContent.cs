using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeVersionContent</c>: a version, its mappings, the allocation summary and the rule findings.</summary>
public sealed record BridgeVersionContent
{
    /// <summary>The version.</summary>
    public required BridgeVersion Version { get; init; }

    /// <summary>The mappings, by source member key, then target member key.</summary>
    public required IReadOnlyList<BridgeMapping> Mappings { get; init; }

    /// <summary>How each source member allocates.</summary>
    public required BridgeAllocationSummary Allocation { get; init; }

    /// <summary>
    /// The rule findings on this content: <c>target.itemId</c> names the mapping at fault, or <c>target.itemKey</c> the source member of
    /// a per-source rule; empty on a saved live version.
    /// </summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}
