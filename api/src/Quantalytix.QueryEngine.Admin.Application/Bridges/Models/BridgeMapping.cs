namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeMapping</c>: one source member to target member assignment of a version.</summary>
public sealed record BridgeMapping
{
    /// <summary>The mapping's id; in a preview, the temporary id of a mapping the change set created.</summary>
    public required string Id { get; init; }

    /// <summary>The bridge version.</summary>
    public required long VersionId { get; init; }

    /// <summary>The source member's key.</summary>
    public required string SourceMemberKey { get; init; }

    /// <summary>The target member's key.</summary>
    public required string TargetMemberKey { get; init; }

    /// <summary>The coefficient measures with <c>bridge_factor_behavior = value</c> apply (B7).</summary>
    public required decimal Factor { get; init; }

    /// <summary>The share of the source's value the target takes.</summary>
    public required decimal AllocationWeight { get; init; }

    /// <summary>A <c>source_selection</c> value: the source member alone, or with every member under it (report bridges only, B8).</summary>
    public required string SourceSelection { get; init; }
}
