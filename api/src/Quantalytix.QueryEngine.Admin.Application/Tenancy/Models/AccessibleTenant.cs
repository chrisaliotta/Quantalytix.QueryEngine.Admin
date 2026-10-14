namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>A tenant the caller may administer. It deliberately omits the tenant's connection string.</summary>
public sealed record AccessibleTenant
{
    /// <summary>The tenant identity used in tenant-scoped routes.</summary>
    public required string TenantId { get; init; }

    /// <summary>Whether query routing is enabled for the tenant.</summary>
    public required bool IsEnabled { get; init; }
}
