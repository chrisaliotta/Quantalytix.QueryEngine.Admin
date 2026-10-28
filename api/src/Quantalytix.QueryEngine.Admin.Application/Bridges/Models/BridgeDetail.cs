using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Mappings;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeDetail</c>: the list item plus the assumption sets that pin its versions.</summary>
public sealed record BridgeDetail : BridgeListItem
{
    /// <summary>A list item with its pins.</summary>
    /// <param name="item">The list item.</param>
    /// <param name="pins">The assumption set pins.</param>
    [SetsRequiredMembers]
    public BridgeDetail(BridgeListItem item, IReadOnlyList<MappingAssumptionPin> pins)
        : base(item)
    {
        Pins = pins;
    }

    /// <summary>The assumption sets that pin a version, the active set first.</summary>
    public required IReadOnlyList<MappingAssumptionPin> Pins { get; init; }
}
