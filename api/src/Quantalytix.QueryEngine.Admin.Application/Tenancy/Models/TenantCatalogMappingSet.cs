namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogMappingSet</c>.</summary>
public sealed record TenantCatalogMappingSet
{
    /// <summary>The mapping set's id.</summary>
    public required long Id { get; init; }

    /// <summary>The mapping set key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dimension the mapping set maps into.</summary>
    public required long DimensionId { get; init; }
}
