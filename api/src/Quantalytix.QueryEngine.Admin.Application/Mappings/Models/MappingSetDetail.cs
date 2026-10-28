using System.Diagnostics.CodeAnalysis;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingSetDetail</c>: the list item plus the assumption sets that pin its versions.</summary>
public sealed record MappingSetDetail : MappingSetListItem
{
    /// <summary>A list item with its pins.</summary>
    /// <param name="item">The list item.</param>
    /// <param name="pins">The assumption set pins.</param>
    [SetsRequiredMembers]
    public MappingSetDetail(MappingSetListItem item, IReadOnlyList<MappingAssumptionPin> pins)
        : base(item)
    {
        Pins = pins;
    }

    /// <summary>The assumption sets that pin a version, by set key.</summary>
    public required IReadOnlyList<MappingAssumptionPin> Pins { get; init; }
}
