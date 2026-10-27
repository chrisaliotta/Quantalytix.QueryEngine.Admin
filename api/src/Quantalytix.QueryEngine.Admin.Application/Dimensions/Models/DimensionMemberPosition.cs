namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionMemberPosition</c>: where a moved member lands.</summary>
public sealed record DimensionMemberPosition
{
    /// <summary>The new parent, stored or temporary; null for a root.</summary>
    public required string? ParentId { get; init; }

    /// <summary>The index among the new siblings after the move.</summary>
    public required int Index { get; init; }
}
