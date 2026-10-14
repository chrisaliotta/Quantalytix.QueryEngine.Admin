using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>A tenant's metadata at a glance: its identity and revision, the active release, its counts and its drafts.</summary>
public sealed record TenantOverview
{
    /// <summary>The tenant-local identity marker, <c>qe_database_info.tenant_key</c>.</summary>
    public required string TenantKey { get; init; }

    /// <summary>The database's metadata contract version.</summary>
    public required int MetadataSchemaVersion { get; init; }

    /// <summary>The revision the query engine keys its metadata cache by.</summary>
    public required long SemanticRevision { get; init; }

    /// <summary>The release runtime queries use; null when the tenant has none.</summary>
    public SemanticReleaseRecord? ActiveRelease { get; init; }

    /// <summary>How much metadata the tenant holds.</summary>
    public required OverviewCounts Counts { get; init; }

    /// <summary>The drafts in progress.</summary>
    public required OverviewDrafts Drafts { get; init; }
}
