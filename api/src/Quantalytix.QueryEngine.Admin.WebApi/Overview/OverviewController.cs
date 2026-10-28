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
public sealed class OverviewController(OverviewService overview) : ControllerBase
{
    /// <summary>Read the tenant's state, counts and work in progress.</summary>
    /// <param name="tenantId">The tenant to summarize.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The tenant overview.</returns>
    /// <response code="200">The tenant overview.</response>
    [HttpGet(Name = "getOverview")]
    [ProducesResponseType<TenantOverview>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantOverview>> Get(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await overview.GetAsync(cancellationToken));
    }
}
