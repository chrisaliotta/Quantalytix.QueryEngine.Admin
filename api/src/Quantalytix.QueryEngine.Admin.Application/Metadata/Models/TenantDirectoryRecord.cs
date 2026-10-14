namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>query_control.qe_tenant_directory</c> row: maps a trusted tenant identity to its provider and complete tenant connection.</summary>
public sealed record TenantDirectoryRecord
{
    /// <summary>Trusted tenant identity; the primary key.</summary>
    public required string TenantId { get; init; }

    /// <summary>Registered provider identity, such as <c>starrocks</c>.</summary>
    public required string ProviderId { get; init; }

    /// <summary>Literal connection string including the tenant database. It can contain credentials, so it never leaves the server.</summary>
    public required string ConnectionString { get; init; }

    /// <summary>Whether query routing is permitted for the tenant.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Suggested metadata cache lifetime, in seconds.</summary>
    public required int MetadataCacheSeconds { get; init; }

    /// <summary>Last routing-record update time (UTC).</summary>
    public required DateTime UpdatedAt { get; init; }
}
