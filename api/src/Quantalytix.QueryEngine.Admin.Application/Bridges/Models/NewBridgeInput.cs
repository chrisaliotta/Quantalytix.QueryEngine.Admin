namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>A new bridge, as <c>POST bridges</c> takes it: its fixed key and endpoint dimensions, plus its identity attributes.</summary>
public sealed record NewBridgeInput : BridgeInput
{
    /// <summary>Stable key, unique across bridges and fixed once created.</summary>
    public required string BridgeKey { get; init; }

    /// <summary>The canonical dimension the bridge remaps from; fixed once created.</summary>
    public required long SourceDimensionId { get; init; }

    /// <summary>The canonical dimension the bridge remaps to; fixed once created.</summary>
    public required long TargetDimensionId { get; init; }
}
