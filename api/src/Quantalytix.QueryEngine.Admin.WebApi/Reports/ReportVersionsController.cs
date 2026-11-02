using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>Report versions (blueprint 16): the whole document of a live or frozen version, which a snapshot's entries name.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/report-versions/{reportVersionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class ReportVersionsController(ReportQueries queries) : ControllerBase
{
    /// <summary>Reads a report version's whole document.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="reportVersionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The document.</returns>
    /// <response code="200">The version as stored.</response>
    [HttpGet]
    [ProducesResponseType<ReportDocument>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportDocument>> Get(string tenantId, long reportVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.VersionDocumentAsync(reportVersionId, cancellationToken));
    }
}
