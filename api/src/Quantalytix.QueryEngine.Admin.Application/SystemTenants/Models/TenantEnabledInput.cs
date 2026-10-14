namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>A request to permit or stop query routing for a tenant.</summary>
public sealed record TenantEnabledInput
{
    /// <summary>Whether query routing is permitted for the tenant.</summary>
    public required bool IsEnabled { get; init; }
}
