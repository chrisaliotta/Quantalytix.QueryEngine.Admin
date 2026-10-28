namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeAllocationSummary</c>: how each source member of a version allocates.</summary>
public sealed record BridgeAllocationSummary
{
    /// <summary>Every source member the version maps, by key in ordinal order.</summary>
    public required IReadOnlyList<BridgeSourceAllocation> Sources { get; init; }
}
