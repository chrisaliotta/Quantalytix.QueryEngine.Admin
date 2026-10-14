using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Tests.Support;

/// <summary>
/// A tenant context fixed to one tenant, for tests that compose services outside an HTTP request, where no route names the
/// tenant.
/// </summary>
/// <param name="tenantId">The tenant every tenant table gateway opens, such as <c>sandbox</c>.</param>
public sealed class FixedTenantContext(string tenantId) : ITenantContext
{
    /// <summary>The fixed tenant.</summary>
    public string TenantId { get; } = tenantId;
}
