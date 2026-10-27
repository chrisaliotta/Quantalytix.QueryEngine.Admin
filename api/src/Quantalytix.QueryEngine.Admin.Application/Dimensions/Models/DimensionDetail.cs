using System.Diagnostics.CodeAnalysis;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionDetail</c>: the list item plus what references the dimension, its roles and its member keys.</summary>
public sealed record DimensionDetail : DimensionListItem
{
    /// <summary>A list item with its usage.</summary>
    /// <param name="item">The list item.</param>
    /// <param name="usage">What references the dimension.</param>
    [SetsRequiredMembers]
    public DimensionDetail(DimensionListItem item, IReadOnlyList<DimensionUsageReference> usage)
        : base(item)
    {
        Usage = usage;
    }

    /// <summary>The bindings, mapping sets, bridges, calculation dimensions, report scenarios, report sources and assumption set pins.</summary>
    public required IReadOnlyList<DimensionUsageReference> Usage { get; init; }
}
