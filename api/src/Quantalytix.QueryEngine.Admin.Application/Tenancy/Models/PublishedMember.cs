namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>PublishedMember</c>: a member of a dimension's live version, named by keys.</summary>
public sealed record PublishedMember
{
    /// <summary>The member key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The parent member's key; null for a root.</summary>
    public required string? ParentKey { get; init; }

    /// <summary>Whether bindings and mappings may assign rows to the member.</summary>
    public required bool IsAssignable { get; init; }

    /// <summary>The member's level key; null when it has no level.</summary>
    public required string? LevelKey { get; init; }
}
