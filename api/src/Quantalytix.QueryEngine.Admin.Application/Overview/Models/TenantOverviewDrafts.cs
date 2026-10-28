namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>
/// The contract's <c>TenantOverviewDrafts</c>: the work in progress of the kinds that still have a draft status. Dimensions,
/// mapping sets and bridges follow the living lifecycle and have no drafts (OI-14).
/// </summary>
public sealed record TenantOverviewDrafts
{
    /// <summary>Draft dataset versions.</summary>
    public required int DatasetVersions { get; init; }

    /// <summary>Draft releases.</summary>
    public required int Releases { get; init; }
}
