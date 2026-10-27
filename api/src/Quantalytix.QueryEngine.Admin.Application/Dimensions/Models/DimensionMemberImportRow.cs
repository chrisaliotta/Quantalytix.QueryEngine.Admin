namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionMemberImportRow</c>: one row the browser parsed (OI-21).</summary>
public sealed record DimensionMemberImportRow
{
    /// <summary>The member key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name; empty takes the key.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The parent's key, in the import or the version; null for a root.</summary>
    public required string? ParentKey { get; init; }
}
