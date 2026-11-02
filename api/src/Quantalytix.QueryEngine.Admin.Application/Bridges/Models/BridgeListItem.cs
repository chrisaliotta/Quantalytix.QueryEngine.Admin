using Quantalytix.QueryEngine.Admin.Application.Mappings;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The contract's <c>BridgeListItem</c>: an analytical or report bridge with its endpoints, version summaries and the bindings that
/// use it.
/// </summary>
public sealed record BridgeListItem
{
    /// <summary>The bridge's id.</summary>
    public required long Id { get; init; }

    /// <summary>The bridge key, fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dimension the bridge maps from, fixed after creation.</summary>
    public required long SourceDimensionId { get; init; }

    /// <summary>The dimension the bridge maps into, fixed after creation.</summary>
    public required long TargetDimensionId { get; init; }

    /// <summary>Whether queries may use the bridge: the live version's flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The report source of a report bridge, which is written only through its report; null for an analytical bridge.</summary>
    public required BridgeOwner? Owner { get; init; }

    /// <summary>The source dimension.</summary>
    public required MappingDimensionRef Source { get; init; }

    /// <summary>The target dimension.</summary>
    public required MappingDimensionRef Target { get; init; }

    /// <summary>Every version, one per snapshot holding the bridge: the live one first, then frozen ones, newest first.</summary>
    public required IReadOnlyList<BridgeVersionSummary> Versions { get; init; }

    /// <summary>The live version, which change sets edit.</summary>
    public required long LiveVersionId { get; init; }

    /// <summary>The mappings of the live version.</summary>
    public required int LiveMappingCount { get; init; }

    /// <summary>The live dataset bindings that remap through the bridge.</summary>
    public required IReadOnlyList<MappingBindingUsage> Bindings { get; init; }
}
