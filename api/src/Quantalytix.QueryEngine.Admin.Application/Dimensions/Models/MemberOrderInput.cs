namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>A reparent or reorder of a draft version's member tree, as <c>PUT dimension-versions/{dimensionVersionId}/members/order</c> takes it.</summary>
public sealed record MemberOrderInput
{
    /// <summary>The placements to store, one per member; members whose parent and sort order are unchanged are not rewritten.</summary>
    public required IReadOnlyList<MemberPlacement> Items { get; init; }
}
