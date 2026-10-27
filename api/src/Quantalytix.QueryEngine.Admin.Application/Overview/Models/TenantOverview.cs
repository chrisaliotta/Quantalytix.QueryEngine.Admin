using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>The contract's <c>TenantOverview</c>: the tenant info with its counts and its work in progress.</summary>
public sealed record TenantOverview : TenantInfo
{
    /// <summary>The tenant info with its counts and drafts.</summary>
    /// <param name="info">The tenant info.</param>
    /// <param name="counts">How much metadata the tenant holds.</param>
    /// <param name="drafts">The drafts in progress.</param>
    [SetsRequiredMembers]
    public TenantOverview(TenantInfo info, TenantOverviewCounts counts, TenantOverviewDrafts drafts)
        : base(info)
    {
        Counts = counts;
        Drafts = drafts;
    }

    /// <summary>How much metadata the tenant holds.</summary>
    public required TenantOverviewCounts Counts { get; init; }

    /// <summary>The drafts in progress.</summary>
    public required TenantOverviewDrafts Drafts { get; init; }
}
