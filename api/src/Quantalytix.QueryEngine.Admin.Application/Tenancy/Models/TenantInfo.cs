namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantInfo</c>: the tenant database's identity and the revision the engine keys its metadata cache by.</summary>
public record TenantInfo
{
    /// <summary>The tenant-local identity marker, <c>qe_database_info.tenant_key</c>.</summary>
    public required string TenantKey { get; init; }

    /// <summary>The database's metadata contract version.</summary>
    public required int MetadataSchemaVersion { get; init; }

    /// <summary>Advances once per commit or command that changes what the engine reads.</summary>
    public required long SemanticRevision { get; init; }

    /// <summary>The release runtime queries use; null when the tenant has none.</summary>
    public required long? ActiveReleaseId { get; init; }
}
