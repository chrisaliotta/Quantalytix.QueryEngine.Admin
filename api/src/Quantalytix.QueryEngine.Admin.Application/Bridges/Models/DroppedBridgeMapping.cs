namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>A mapping a bridge version clone could not carry over, named by the member keys of the version it was cloned from.</summary>
public sealed record DroppedBridgeMapping
{
    /// <summary>The key of the mapping's source member.</summary>
    public required string SourceMemberKey { get; init; }

    /// <summary>The key of the mapping's target member.</summary>
    public required string TargetMemberKey { get; init; }
}
