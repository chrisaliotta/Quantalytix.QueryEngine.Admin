namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogSnapshot</c>: a snapshot a request or a comparison can name.</summary>
public sealed record TenantCatalogSnapshot
{
    /// <summary>The snapshot's id.</summary>
    public required long Id { get; init; }

    /// <summary>The key requests name the snapshot by.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether this is the live snapshot.</summary>
    public required bool IsLive { get; init; }
}
