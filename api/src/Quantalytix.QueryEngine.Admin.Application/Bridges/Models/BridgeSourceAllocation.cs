namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeSourceAllocation</c>: one source member's targets and the sum of its allocation weights.</summary>
public sealed record BridgeSourceAllocation
{
    /// <summary>The source member's key.</summary>
    public required string SourceMemberKey { get; init; }

    /// <summary>The mappings from it.</summary>
    public required int TargetCount { get; init; }

    /// <summary>The sum of their allocation weights, in decimal.</summary>
    public required decimal WeightSum { get; init; }
}
