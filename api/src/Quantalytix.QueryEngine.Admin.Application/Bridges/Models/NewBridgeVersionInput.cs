namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// A new draft version of a bridge, as <c>POST bridges/{bridgeId}/versions</c> takes it: its endpoint versions and allocation
/// semantics, and optionally a version whose mappings it copies.
/// </summary>
public sealed record NewBridgeVersionInput : BridgeVersionInput
{
    /// <summary>
    /// The bridge version whose mappings the draft copies, each moved to the members with the same keys in the new endpoint
    /// versions; null for an empty draft.
    /// </summary>
    public long? CloneFromVersionId { get; init; }
}
