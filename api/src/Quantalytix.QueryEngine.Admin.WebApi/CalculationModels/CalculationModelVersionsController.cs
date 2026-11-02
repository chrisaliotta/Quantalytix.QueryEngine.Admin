using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;

namespace Quantalytix.QueryEngine.Admin.WebApi.CalculationModels;

/// <summary>Calculation model versions (blueprint 15): the whole document of a live or frozen version, which a snapshot's entries name.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/calculation-model-versions/{modelVersionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class CalculationModelVersionsController(ModelQueries queries) : ControllerBase
{
    /// <summary>Reads a model version's whole document.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="modelVersionId">The model version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The document.</returns>
    /// <response code="200">The version with its variables, measures, parameters, attributions, effective drivers and readers.</response>
    [HttpGet]
    [ProducesResponseType<ModelDocument>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModelDocument>> Get(string tenantId, long modelVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.VersionDocumentAsync(modelVersionId, cancellationToken));
    }
}
