namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The editable identity attributes of a bridge, as <c>PUT bridges/{bridgeId}</c> takes them.</summary>
public record BridgeInput
{
    /// <summary>Human-readable name, at most 256 characters.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Administrative availability.</summary>
    public required bool IsEnabled { get; init; }
}
