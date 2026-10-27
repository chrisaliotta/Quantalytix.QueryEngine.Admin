namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// The contract's <c>TenantCatalog</c>: the names and ids every module's pickers and cross-module links read (OI-13), each list
/// by key. Report-owned dimensions and bridges are included only so references resolve (OI-22).
/// </summary>
public sealed record TenantCatalog
{
    /// <summary>The tenant's identity and revision, which clients cache the catalog on.</summary>
    public required TenantInfo Tenant { get; init; }

    /// <summary>Every dataset.</summary>
    public required IReadOnlyList<TenantCatalogDataset> Datasets { get; init; }

    /// <summary>Every dimension, analytical and report.</summary>
    public required IReadOnlyList<TenantCatalogDimension> Dimensions { get; init; }

    /// <summary>Every dimension role.</summary>
    public required IReadOnlyList<TenantCatalogRole> Roles { get; init; }

    /// <summary>Every mapping set.</summary>
    public required IReadOnlyList<TenantCatalogMappingSet> MappingSets { get; init; }

    /// <summary>Every bridge, analytical and report.</summary>
    public required IReadOnlyList<TenantCatalogBridge> Bridges { get; init; }

    /// <summary>Every calculation model.</summary>
    public required IReadOnlyList<TenantCatalogModel> Models { get; init; }

    /// <summary>Every report.</summary>
    public required IReadOnlyList<TenantCatalogReport> Reports { get; init; }

    /// <summary>Every release.</summary>
    public required IReadOnlyList<TenantCatalogRelease> Releases { get; init; }

    /// <summary>Every assumption set.</summary>
    public required IReadOnlyList<TenantCatalogAssumptionSet> AssumptionSets { get; init; }
}
