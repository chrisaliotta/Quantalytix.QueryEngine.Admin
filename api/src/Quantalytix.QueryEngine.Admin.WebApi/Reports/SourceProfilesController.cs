using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists and edits the source profiles of reports: the source each variable reads under the profile.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports/{reportId:long}/profiles")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class SourceProfilesController(IReportContent<SourceProfile, SourceProfileInput> profiles, ReportChanges changes) : ControllerBase
{
    /// <summary>Lists a report's source profiles.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The source profiles by key.</returns>
    /// <response code="200">The profiles by key, ignoring case, each with its picks by variable.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SourceProfile>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SourceProfile>>> List(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await profiles.ListAsync(reportId, cancellationToken));
    }

    /// <summary>Adds a source profile with its picks to a report.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The new profile.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created profile.</returns>
    /// <response code="200">The created profile.</response>
    /// <response code="400">The input breaks a field rule, such as a pick of a source that does not supply the variable; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the profile would break its rules, such as an enabled profile missing a pick; nothing was written.</response>
    [HttpPost]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<SourceProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<SourceProfile>> Create(string tenantId, long reportId, SourceProfileInput input, CancellationToken cancellationToken)
    {
        var created = profiles.Compose(reportId, null, input);
        var violations = await changes.ApplyAsync(reportId, report => profiles.Put(report, created), () => profiles.InsertAsync(created, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, reportId, violations);
        return Ok(created);
    }

    /// <summary>Replaces a source profile of a report and all its picks, keeping the profile's ID.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="profile">The report and its profile.</param>
    /// <param name="input">The profile's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored profile.</returns>
    /// <response code="200">The stored profile.</response>
    /// <response code="400">The profile is not the report's, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the profile would break its rules; nothing was written.</response>
    [HttpPut("{itemId:long}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<SourceProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<SourceProfile>> Update(
        string tenantId,
        [FromRoute] ReportItemReference<SourceProfile> profile,
        SourceProfileInput input,
        CancellationToken cancellationToken)
    {
        var updated = profiles.Compose(profile.ReportId, profile.ItemId, input);
        var violations = await changes.ApplyAsync(
            profile.ReportId, report => profiles.Put(report, updated), () => profiles.UpdateAsync(updated, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, profile.ReportId, violations);
        return Ok(updated);
    }

    /// <summary>Deletes a source profile of a report with its picks, unless the report runs under it by default.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="profile">The report and its profile.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when nothing was deleted.</returns>
    /// <response code="204">The profile is deleted.</response>
    /// <response code="400">The profile is not the report's; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the profile is its report's default.</response>
    /// <response code="422">The report is enabled and losing the profile would break its rules; nothing was deleted.</response>
    [HttpDelete("{itemId:long}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] ReportItemReference<SourceProfile> profile, CancellationToken cancellationToken)
    {
        string? conflict = null;
        var violations = await changes.ApplyAsync(
            profile.ReportId,
            report => profiles.Without(report, profile.ItemId),
            async () => conflict = await profiles.DeleteAsync(profile.ItemId, cancellationToken),
            cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, profile.ReportId, violations);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
