namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeEditState</c>: the bridge change-set aggregate as the editor shows it.</summary>
public sealed record BridgeEditState
{
    /// <summary>The bridge with its endpoints, versions and bindings.</summary>
    public required BridgeListItem Bridge { get; init; }

    /// <summary>The live version's content.</summary>
    public required BridgeVersionContent Content { get; init; }
}
