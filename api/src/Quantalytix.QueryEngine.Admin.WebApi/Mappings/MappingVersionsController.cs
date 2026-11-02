using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Mappings;

/// <summary>
/// Mapping versions (blueprint 13): the content of a live or frozen version with its rule findings.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/mapping-versions/{mappingVersionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class MappingVersionsController(MappingQueries queries) : ControllerBase
{
    /// <summary>Reads a version with its entries, coverage and rule findings.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version content.</returns>
    /// <response code="200">The version, its entries, coverage and rule findings.</response>
    [HttpGet]
    [ProducesResponseType<MappingVersionContent>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MappingVersionContent>> Get(string tenantId, long mappingVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ContentAsync(mappingVersionId, cancellationToken));
    }
}
