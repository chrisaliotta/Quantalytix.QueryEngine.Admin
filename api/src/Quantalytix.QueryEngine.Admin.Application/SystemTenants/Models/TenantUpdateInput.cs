namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>An edit of a tenant directory row; the tenant ID is fixed and enabling has its own request.</summary>
public sealed record TenantUpdateInput
{
    /// <summary>The registered provider, a <c>provider_id</c> value.</summary>
    public required string ProviderId { get; init; }

    /// <summary>A new connection string, which is verified before it is stored; null keeps the current one.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>The suggested metadata cache lifetime, in seconds.</summary>
    public required int MetadataCacheSeconds { get; init; }
}
