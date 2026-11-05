namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>The <c>qe_database_info</c> row: the singleton tenant metadata record whose revision keys the engine's metadata cache.</summary>
public sealed record DatabaseInfoRecord
{
    /// <summary>Always <c>1</c>.</summary>
    public required int SingletonId { get; init; }

    /// <summary>Tenant-local identity marker.</summary>
    public required string TenantKey { get; init; }

    /// <summary>Database metadata contract version (currently <c>21</c>).</summary>
    public required int MetadataSchemaVersion { get; init; }

    /// <summary>Monotonic revision the query engine uses to invalidate cached metadata.</summary>
    public required long SemanticRevision { get; init; }

    /// <summary>Last metadata change (UTC).</summary>
    public required DateTime UpdatedAt { get; init; }
}
