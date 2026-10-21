using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.ReportDesigns;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>
/// Lists, creates, clones, edits and deletes report versions. A report's draft is created and deleted with the drafts of its
/// report dimension and bridges; status changes of the set go through the report design status endpoint.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportVersionsController(ReportVersionService versions, ReportDesignDrafts drafts) : ControllerBase
{
    /// <summary>Lists a report's versions with their report dimension, model version, defaults and status.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The versions by version number.</returns>
    /// <response code="200">The versions by version number.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("reports/{reportId:long}/versions")]
    [ProducesResponseType<IReadOnlyList<ReportVersionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportVersionRecord>>> List(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await versions.ListAsync(reportId, cancellationToken));
    }

    /// <summary>
    /// Creates the report's draft with its next version number, empty or with copies of the content of <c>cloneFromVersionId</c>,
    /// and drafts of its report dimension and of every bridge into it that copy their published versions.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report, which has no draft.</param>
    /// <param name="input">The calculation model, defaults and optional clone source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created draft.</returns>
    /// <response code="200">The created draft.</response>
    /// <response code="400">The input breaks a field rule, such as a clone source of another report or a report that already has a draft; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost("reports/{reportId:long}/versions")]
    [ProducesResponseType<ReportVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ReportVersionRecord>> Create(
        string tenantId,
        long reportId,
        NewReportVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await drafts.CreateAsync(reportId, input, cancellationToken));
    }

    /// <summary>Reads one report version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version.</returns>
    /// <response code="200">The version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("report-versions/{versionId:long}")]
    [ProducesResponseType<ReportVersionRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportVersionRecord>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await versions.GetAsync(versionId, cancellationToken));
    }

    /// <summary>
    /// Replaces a draft or published version's default profile, payload and defaults; its report dimension, model, lines and
    /// columns stay as they are.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored version.</returns>
    /// <response code="200">The stored version.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("report-versions/{versionId:long}")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<ReportVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ReportVersionRecord>> Update(
        string tenantId,
        long versionId,
        ReportVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.UpdateAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Deletes the report's draft with its content and the drafts of its bridges; the report dimension's draft goes too once the
    /// dimension has a published version.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the version is not a draft.</returns>
    /// <response code="204">The draft and the drafts paired with it are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft.</response>
    [HttpDelete("report-versions/{versionId:long}")]
    [RequireDraft(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        await drafts.DeleteAsync(versionId, cancellationToken);
        return NoContent();
    }
}
