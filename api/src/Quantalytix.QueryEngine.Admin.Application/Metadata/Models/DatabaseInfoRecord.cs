namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>The <c>qe_database_info</c> row: the singleton tenant metadata record that points runtime queries at the active release.</summary>
public sealed record DatabaseInfoRecord
{
    /// <summary>Always <c>1</c>.</summary>
    public required int SingletonId { get; init; }

    /// <summary>Tenant-local identity marker.</summary>
    public required string TenantKey { get; init; }

    /// <summary>Database metadata contract version (currently <c>4</c>).</summary>
    public required int MetadataSchemaVersion { get; init; }

    /// <summary>Monotonic revision the query engine uses to invalidate cached metadata.</summary>
    public required long SemanticRevision { get; init; }

    /// <summary>The runtime-active <c>qe_semantic_release.semantic_release_id</c>.</summary>
    public long? ActiveSemanticReleaseId { get; init; }

    /// <summary>Last publication metadata change (UTC).</summary>
    public required DateTime UpdatedAt { get; init; }
}
