using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>A new draft bridge version, with the mappings a clone could not carry over.</summary>
public sealed record CreatedBridgeVersion
{
    /// <summary>The new draft.</summary>
    public required BridgeVersionRecord Version { get; init; }

    /// <summary>
    /// The cloned mappings whose source or target member key no member of the new endpoint version has, by source key, then
    /// target key; empty for an empty draft.
    /// </summary>
    public required IReadOnlyList<DroppedBridgeMapping> DroppedMappings { get; init; }
}
