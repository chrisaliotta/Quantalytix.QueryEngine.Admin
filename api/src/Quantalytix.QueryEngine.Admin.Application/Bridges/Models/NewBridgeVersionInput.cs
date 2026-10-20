namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// A new draft version of a bridge, as <c>POST bridges/{bridgeId}/versions</c> takes it: its allocation semantics, and
/// optionally a version whose mappings it copies.
/// </summary>
public sealed record NewBridgeVersionInput : BridgeVersionInput
{
    /// <summary>A version of the same bridge whose mappings the draft copies; null for an empty draft.</summary>
    public long? CloneFromVersionId { get; init; }
}
