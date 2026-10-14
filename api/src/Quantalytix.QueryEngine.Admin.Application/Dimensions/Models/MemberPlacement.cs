namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Where one member sits in the tree after a drag-and-drop: its parent and its order among its siblings.</summary>
public sealed record MemberPlacement
{
    /// <summary>The member being placed.</summary>
    public required long MemberId { get; init; }

    /// <summary>The member's new parent; null for a root.</summary>
    public long? ParentMemberId { get; init; }

    /// <summary>The member's new presentation order among its siblings.</summary>
    public required int SortOrder { get; init; }
}
