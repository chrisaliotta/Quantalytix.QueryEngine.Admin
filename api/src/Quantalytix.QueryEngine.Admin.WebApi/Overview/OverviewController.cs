using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.Application.Overview;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Overview;

/// <summary>Summarizes a tenant's metadata for the overview screen.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/overview")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<Problem>(StatusCodes.Status401Unauthorized, ProblemWriter.ContentType)]
[ProducesResponseType<Problem>(StatusCodes.Status403Forbidden, ProblemWriter.ContentType)]
public sealed class OverviewController(OverviewService overview) : ControllerBase
{
    /// <summary>Read the tenant's state, counts and work in progress.</summary>
    /// <param name="tenantId">The tenant to summarize.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The tenant overview.</returns>
    /// <response code="200">The tenant overview.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet(Name = "getOverview")]
    [ProducesResponseType<TenantOverview>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantOverview>> Get(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await overview.GetAsync(cancellationToken));
    }
}
