namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>A member route: the dimension version and one of its members.</summary>
public sealed record MemberReference
{
    /// <summary>The dimension version that owns the member.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>The member.</summary>
    public required long DimensionMemberId { get; init; }
}
