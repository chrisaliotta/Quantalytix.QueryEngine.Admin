using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists and replaces the lines of report versions: the member and formula rows a report shows, as a tree.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/report-versions/{versionId:long}/lines")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportLinesController(ReportLineService lines) : ControllerBase
{
    /// <summary>Lists a report version's lines in pre-order: each line before its children, siblings in display order.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The lines.</returns>
    /// <response code="200">The lines in pre-order.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportLineRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportLineRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await lines.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces a draft report version's lines with the given tree in pre-order; a line that keeps its key keeps its ID.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft report version.</param>
    /// <param name="input">The complete lines.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored lines.</returns>
    /// <response code="200">The lines in pre-order.</response>
    /// <response code="400">A line key repeats, a parent is not an earlier line, or a line's member, measure or column does not resolve; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut]
    [RequireDraft(VersionKinds.ReportVersion, "versionId")]
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
