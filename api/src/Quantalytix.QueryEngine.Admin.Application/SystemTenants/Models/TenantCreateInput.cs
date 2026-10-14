namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>A request to register a tenant in the tenant directory.</summary>
public sealed record TenantCreateInput
{
    /// <summary>The trusted tenant identity, unique and fixed once created; the database's <c>tenant_key</c> must equal it.</summary>
    public required string TenantId { get; init; }

    /// <summary>The registered provider, a <c>provider_id</c> value.</summary>
    public required string ProviderId { get; init; }

    /// <summary>The literal connection string, including the tenant database; it is verified before it is stored.</summary>
    public required string ConnectionString { get; init; }

    /// <summary>Whether query routing is permitted for the tenant.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The suggested metadata cache lifetime, in seconds.</summary>
    public required int MetadataCacheSeconds { get; init; }
}
