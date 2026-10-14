namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>A new member of a draft dimension version, as <c>POST dimension-versions/{dimensionVersionId}/members</c> takes it.</summary>
public sealed record NewMemberInput : MemberInput
{
    /// <summary>
    /// Stable member key, fixed once created: any characters without whitespace at either end, at most 256. Uniqueness within
    /// the version is a publication rule.
    /// </summary>
    public required string MemberKey { get; init; }
}
