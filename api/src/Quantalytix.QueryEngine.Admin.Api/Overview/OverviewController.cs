using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Application.Overview;

namespace Quantalytix.QueryEngine.Admin.Api.Overview;

/// <summary>Summarizes a tenant's metadata for the overview page.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/overview")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class OverviewController(OverviewService overview) : ControllerBase
{
    /// <summary>Reads the tenant's identity and semantic revision, its active release, its counts and its drafts in progress.</summary>
    /// <param name="tenantId">The tenant to summarize.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The tenant overview.</returns>
    /// <response code="200">The tenant overview.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<TenantOverview>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantOverview>> Get(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await overview.GetAsync(cancellationToken));
    }
}
