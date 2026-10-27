using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Tenancy;

/// <summary>Serves the tenant catalog every picker reads (OI-13) and the published members of a dimension.</summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<Problem>(StatusCodes.Status401Unauthorized, ProblemWriter.ContentType)]
[ProducesResponseType<Problem>(StatusCodes.Status403Forbidden, ProblemWriter.ContentType)]
public sealed class TenantCatalogController(TenantCatalogReader catalog, PublishedMembersReader publishedMembers) : ControllerBase
{
    /// <summary>Read the names and ids every picker and cross-module link resolves against.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The tenant catalog.</returns>
    /// <response code="200">The tenant catalog.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("catalog", Name = "getCatalog")]
    [ProducesResponseType<TenantCatalog>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantCatalog>> GetCatalog(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await catalog.ReadAsync(cancellationToken));
    }

    /// <summary>Read the members of a dimension's published version.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The published version's members.</returns>
    /// <response code="200">The published version's members, in hierarchy order.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="404">The dimension does not exist or has no published version.</response>
    [HttpGet("dimensions/{dimensionId:long}/published-members", Name = "getPublishedMembers")]
    [ProducesResponseType<PublishedMembers>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublishedMembers>> GetPublishedMembers(string tenantId, long dimensionId, CancellationToken cancellationToken)
    {
        var members = await publishedMembers.ReadAsync(dimensionId, cancellationToken);
        return members is null ? Problems.OfStatus(StatusCodes.Status404NotFound).ToResult() : Ok(members);
    }
}
