using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists, creates, clones, edits and deletes report versions. Status changes go through the versions status endpoint.</summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportVersionsController(ReportVersionService versions) : ControllerBase
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
    /// Creates a draft with the report's next version number: empty, or cloned from <c>cloneFromVersionId</c>, whose columns
    /// and lines move to the metrics with the same keys in the new model version.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The report dimension, model version, defaults and optional clone source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created draft and the columns and lines the clone dropped.</returns>
    /// <response code="200">The created draft, with the cloned columns and lines whose metrics the new model version lacks.</response>
    /// <response code="400">The input breaks a field rule, such as a clone source of another report; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost("reports/{reportId:long}/versions")]
    [ProducesResponseType<CreatedReportVersion>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CreatedReportVersion>> Create(
        string tenantId,
        long reportId,
        NewReportVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.CreateAsync(reportId, input, cancellationToken));
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
    /// Replaces a draft version's report dimension, default profile and defaults; its model version, lines and columns stay as
    /// they are.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft report version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored version.</returns>
    /// <response code="200">The stored version.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("report-versions/{versionId:long}")]
    [RequireDraft(VersionKinds.ReportVersion, "versionId")]
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
    /// Deletes a draft version that no release pins, with its lines and columns, and clears the report's current-version
    /// pointer when it named the version.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the version is not a draft or a release pins it.</returns>
    /// <response code="204">The version, its lines and its columns are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft, or the detail names the releases that pin it.</response>
    [HttpDelete("report-versions/{versionId:long}")]
    [RequireDraft(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        var conflict = await versions.DeleteAsync(versionId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
