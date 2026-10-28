namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogRole</c>: a dimension role.</summary>
public sealed record TenantCatalogRole
{
    /// <summary>The role's id.</summary>
    public required long Id { get; init; }

    /// <summary>The role key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The role's dimension.</summary>
    public required long DimensionId { get; init; }
}
