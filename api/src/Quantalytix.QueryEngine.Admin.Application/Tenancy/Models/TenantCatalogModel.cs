namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogModel</c>: a calculation model.</summary>
public sealed record TenantCatalogModel
{
    /// <summary>The model's id.</summary>
    public required long Id { get; init; }

    /// <summary>The model key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the model is enabled.</summary>
    public required bool IsEnabled { get; init; }
}
