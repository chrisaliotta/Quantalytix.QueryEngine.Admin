namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>A <c>query_control.qe_tenant_directory</c> row as the API returns it: the connection string's password is masked.</summary>
public sealed record TenantDirectoryEntry
{
    /// <summary>The trusted tenant identity.</summary>
    public required string TenantId { get; init; }

    /// <summary>The registered provider, a <c>provider_id</c> value.</summary>
    public required string ProviderId { get; init; }

    /// <summary>The tenant's connection string with its password replaced by <c>*****</c>.</summary>
    public required string ConnectionString { get; init; }

    /// <summary>Whether query routing is permitted for the tenant.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The suggested metadata cache lifetime, in seconds.</summary>
    public required int MetadataCacheSeconds { get; init; }

    /// <summary>The last routing-record update time (UTC).</summary>
    public required DateTime UpdatedAt { get; init; }
}
