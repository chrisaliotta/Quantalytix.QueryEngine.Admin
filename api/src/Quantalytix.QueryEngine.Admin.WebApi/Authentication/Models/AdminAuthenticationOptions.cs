namespace Quantalytix.QueryEngine.Admin.WebApi.Authentication;

/// <summary>Which claim of a validated bearer token names the caller's tenant, bound from the <c>AdminAuthentication</c> section.</summary>
public sealed record AdminAuthenticationOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "AdminAuthentication";

    /// <summary>The tenant claim type; the query engine's tokens use <c>tenant_id</c>.</summary>
    public string TenantClaimType { get; init; } = "tenant_id";
}
