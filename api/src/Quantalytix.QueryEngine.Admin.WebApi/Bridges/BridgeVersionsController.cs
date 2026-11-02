using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Bridges;

/// <summary>
/// Bridge versions (blueprint 13): the content of a live or frozen version with its rule findings, analytical or report-owned.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/bridge-versions/{bridgeVersionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class BridgeVersionsController(BridgeQueries queries) : ControllerBase
{
    /// <summary>Reads a version with its mappings, allocation summary and rule findings.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version content.</returns>
    /// <response code="200">The version, its mappings, the allocation summary and rule findings.</response>
    [HttpGet]
    [ProducesResponseType<BridgeVersionContent>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BridgeVersionContent>> Get(string tenantId, long bridgeVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ContentAsync(bridgeVersionId, cancellationToken));
    }
}
