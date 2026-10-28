namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The contract's <c>CreateBridgeInput</c>: an analytical bridge, created with an empty live version 1, <c>many_to_one</c> /
/// <c>none</c> (OI-27). Its endpoints are fixed, differ, and are neither time nor report dimensions (B4).
/// </summary>
public sealed record CreateBridgeInput
{
    /// <summary>The bridge key, unique in the tenant and fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dimension the bridge maps from.</summary>
    public required long SourceDimensionId { get; init; }

    /// <summary>The dimension the bridge maps into.</summary>
    public required long TargetDimensionId { get; init; }

    /// <summary>Whether queries may use the bridge.</summary>
    public required bool IsEnabled { get; init; }
}
