namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The editable attributes of a member, as <c>PUT dimension-versions/{dimensionVersionId}/members/{memberId}</c> takes them.</summary>
public record MemberInput
{
    /// <summary>Human-readable label, at most 512 characters.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Optional longer explanation, at most 1024 characters.</summary>
    public string? Description { get; init; }

    /// <summary>The parent member; null for a root. It must belong to the same version, which publication checks.</summary>
    public long? ParentMemberId { get; init; }

    /// <summary>The business level; null for none. It must belong to the same version, which publication checks.</summary>
    public long? LevelId { get; init; }

    /// <summary>Whether source values may map directly to the member; false for folder-only nodes.</summary>
    public required bool IsAssignable { get; init; }

    /// <summary>Presentation order among the member's siblings.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Extensible member metadata as raw JSON text, or null.</summary>
    public string? Properties { get; init; }
}
