namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogBridge</c>: an analytical or report bridge.</summary>
public sealed record TenantCatalogBridge
{
    /// <summary>The bridge's id.</summary>
    public required long Id { get; init; }

    /// <summary>The bridge key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dimension the bridge maps from.</summary>
    public required long SourceDimensionId { get; init; }

    /// <summary>The dimension the bridge maps into.</summary>
    public required long TargetDimensionId { get; init; }

    /// <summary>The report that owns a report bridge; null for an analytical bridge.</summary>
    public required long? OwnerReportId { get; init; }
}
