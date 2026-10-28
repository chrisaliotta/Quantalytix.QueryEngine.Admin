namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogAssumptionSet</c>.</summary>
public sealed record TenantCatalogAssumptionSet
{
    /// <summary>The assumption set's id.</summary>
    public required long Id { get; init; }

    /// <summary>The assumption set key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An <c>assumption_set_status</c> value.</summary>
    public required string Status { get; init; }
}
