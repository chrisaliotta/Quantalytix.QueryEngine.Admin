namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeCreated</c>: a new bridge and its empty live version 1.</summary>
public sealed record BridgeCreated
{
    /// <summary>The new bridge.</summary>
    public required long BridgeId { get; init; }

    /// <summary>Its empty live version 1.</summary>
    public required long VersionId { get; init; }
}
