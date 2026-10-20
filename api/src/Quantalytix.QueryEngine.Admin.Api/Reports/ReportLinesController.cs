using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists and replaces the overlay rows of report versions: the sections and formula rows placed among the rows of the report dimension's members.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/report-versions/{versionId:long}/lines")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportLinesController(ReportLineService lines) : ControllerBase
{
    /// <summary>Lists a report version's overlay rows.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The overlay rows.</returns>
    /// <response code="200">The overlay rows by sort order, then key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportLineRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportLineRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await lines.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces a draft or published report version's overlay rows; a row that carries its ID keeps it.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The complete overlay rows.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored overlay rows.</returns>
    /// <response code="200">The overlay rows by sort order, then key.</response>
    /// <response code="400">A row key repeats, an overlay parent is not an earlier row, or a formula row's measure or column does not resolve; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<IReadOnlyList<ReportLineRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ReportLineRecord>>> Replace(
        string tenantId,
        long versionId,
        ReportLinesInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await lines.ReplaceAsync(versionId, input, cancellationToken));
    }
}
