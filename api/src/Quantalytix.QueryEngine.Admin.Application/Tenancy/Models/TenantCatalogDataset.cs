namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogDataset</c>.</summary>
public sealed record TenantCatalogDataset
{
    /// <summary>The dataset's id.</summary>
    public required long Id { get; init; }

    /// <summary>The dataset key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the live version is enabled.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The live version.</summary>
    public required long LiveVersionId { get; init; }
}
