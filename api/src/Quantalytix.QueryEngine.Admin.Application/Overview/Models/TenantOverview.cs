using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>The contract's <c>TenantOverview</c>: the tenant info with its counts.</summary>
public sealed record TenantOverview : TenantInfo
{
    /// <summary>The tenant info with its counts.</summary>
    /// <param name="info">The tenant info.</param>
    /// <param name="counts">How much metadata the tenant holds.</param>
    [SetsRequiredMembers]
    public TenantOverview(TenantInfo info, TenantOverviewCounts counts)
        : base(info)
    {
        Counts = counts;
    }

    /// <summary>How much metadata the tenant holds.</summary>
    public required TenantOverviewCounts Counts { get; init; }
}
