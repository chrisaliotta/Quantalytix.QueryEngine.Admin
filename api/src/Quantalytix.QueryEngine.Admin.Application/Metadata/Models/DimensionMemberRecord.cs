namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dimension_member</c> row: a canonical dimension member or hierarchy node.</summary>
public sealed record DimensionMemberRecord
{
    /// <summary>Surrogate primary key, unique across all dimension versions.</summary>
    public required long MemberId { get; init; }

    /// <summary>The owning dimension version.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>Stable member key, unique within the dimension version.</summary>
    public required string MemberKey { get; init; }

    /// <summary>Human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Optional longer explanation of the member.</summary>
    public string? Description { get; init; }

    /// <summary>Parent member in the authoritative tree, from the same dimension version.</summary>
    public long? ParentMemberId { get; init; }

    /// <summary>Optional business level.</summary>
    public long? LevelId { get; init; }

    /// <summary>Whether source values may map directly to this member.</summary>
    public required bool IsAssignable { get; init; }

    /// <summary>Presentation ordering within peer groups.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Extensible member metadata as raw JSON text (a JSON column).</summary>
    public string? Properties { get; init; }
}
