namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>PublishedMembers</c>: the members of a dimension's live version, fetched per picker.</summary>
public sealed record PublishedMembers
{
    /// <summary>The dimension.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The live version.</summary>
    public required long VersionId { get; init; }

    /// <summary>The members, in hierarchy order.</summary>
    public required IReadOnlyList<PublishedMember> Members { get; init; }
}
