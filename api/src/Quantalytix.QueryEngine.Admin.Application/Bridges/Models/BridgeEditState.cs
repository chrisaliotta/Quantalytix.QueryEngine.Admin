namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeEditState</c>: the bridge change-set aggregate as the editor shows it.</summary>
public sealed record BridgeEditState
{
    /// <summary>The bridge with its endpoints, versions, bindings and pins.</summary>
    public required BridgeDetail Bridge { get; init; }

    /// <summary>The live version's content.</summary>
    public required BridgeVersionContent Content { get; init; }
}
