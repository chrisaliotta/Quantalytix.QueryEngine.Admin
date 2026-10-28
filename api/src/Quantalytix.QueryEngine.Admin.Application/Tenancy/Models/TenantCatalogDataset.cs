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

    /// <summary>Whether the dataset is enabled.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The dataset version the active release pins; null when it pins none.</summary>
    public required long? ActiveVersionId { get; init; }
}
