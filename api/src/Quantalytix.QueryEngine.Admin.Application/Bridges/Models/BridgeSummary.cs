using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// A bridge as the Bridges section lists it: with its purpose, so analytical bridges can be told from the bridges report sources
/// own, and the owner of a report bridge, whose report editor is the only place it is edited.
/// </summary>
public sealed record BridgeSummary
{
    /// <summary>The bridge.</summary>
    public required BridgeRecord Bridge { get; init; }

    /// <summary>The <c>dimension_purpose</c> of the bridge's target dimension: <c>report</c> for a report source's bridge.</summary>
    public required string Purpose { get; init; }

    /// <summary>The report source that owns a report bridge; null for an analytical bridge.</summary>
    public BridgeOwner? Owner { get; init; }
}
