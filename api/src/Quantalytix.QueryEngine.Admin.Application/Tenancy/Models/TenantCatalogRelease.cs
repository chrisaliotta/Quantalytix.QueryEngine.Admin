namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogRelease</c>: a release has a key and no display name (OI-39).</summary>
public sealed record TenantCatalogRelease
{
    /// <summary>The release's id.</summary>
    public required long Id { get; init; }

    /// <summary>The release key.</summary>
    public required string Key { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Whether runtime queries use the release.</summary>
    public required bool IsActive { get; init; }
}
