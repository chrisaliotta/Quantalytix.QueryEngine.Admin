using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The contract's <c>Grouping</c> union, discriminated by <c>mode</c>: how a role's members group.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "mode")]
[JsonDerivedType(typeof(GroupingMember), "member")]
[JsonDerivedType(typeof(GroupingLevel), "level")]
[JsonDerivedType(typeof(GroupingAncestor), "ancestor")]
[JsonDerivedType(typeof(GroupingDepth), "depth")]
public abstract record Grouping;

/// <summary><c>member</c>: by the member itself.</summary>
public sealed record GroupingMember : Grouping;

/// <summary><c>level</c>: by each member's ancestor at a level.</summary>
public sealed record GroupingLevel : Grouping
{
    /// <summary>The level key.</summary>
    public required string LevelKey { get; init; }
}

/// <summary><c>ancestor</c>: a subtree grouped at a fixed ancestor member.</summary>
public sealed record GroupingAncestor : Grouping
{
    /// <summary>The ancestor member's key.</summary>
    public required string MemberKey { get; init; }
}

/// <summary><c>depth</c>: by each member's ancestor at a closure depth.</summary>
public sealed record GroupingDepth : Grouping
{
    /// <summary>The closure depth.</summary>
    public required int Depth { get; init; }
}
