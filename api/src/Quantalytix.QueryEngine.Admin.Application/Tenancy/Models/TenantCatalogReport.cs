namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogReport</c>.</summary>
public sealed record TenantCatalogReport
{
    /// <summary>The report's id.</summary>
    public required long Id { get; init; }

    /// <summary>The report key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the report is enabled.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The calculation model the report evaluates.</summary>
    public required long ModelId { get; init; }

    /// <summary>The report dimension that holds the report's rows.</summary>
    public required long ReportDimensionId { get; init; }
}
