using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>
/// Lists, creates, edits, enables and deletes reports: the row and column layouts the engine executes over a model. The engine
/// sees enabled reports only, so enabling one checks it against the report rules, and every write to an enabled report is checked
/// before it is made.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportsController(ReportService reports, ReportStatusService statuses) : ControllerBase
{
    /// <summary>Lists every report of the tenant.</summary>
    /// <param name="tenantId">The tenant that owns the reports.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The reports by key.</returns>
    /// <response code="200">The reports by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportRecord>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await reports.ListAsync(cancellationToken));
    }

    /// <summary>
    /// Creates a disabled report over a calculation model, and its own report dimension, keyed and named like it,
    /// whose one version copies the published members of <c>startFromDimensionId</c> or holds a single root row and is published
    /// at once.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="input">The new report's key, model, display name, type, payload and optional starting dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created report.</returns>
    /// <response code="200">The created report.</response>
    /// <response code="400">The input breaks a field rule, such as a taken key or an unknown model; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<ReportRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ReportRecord>> Create(string tenantId, NewReportInput input, CancellationToken cancellationToken)
    {
        return Ok(await reports.CreateAsync(input, cancellationToken));
    }

    /// <summary>Reads one report.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The report.</returns>
    /// <response code="200">The report.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{reportId:long}")]
    [ProducesResponseType<ReportRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportRecord>> Get(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await reports.GetAsync(reportId, cancellationToken));
    }

    /// <summary>Edits a report's display name, type and payload; the semantic revision advances when the report is enabled.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored report.</returns>
    /// <response code="200">The stored report.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPut("{reportId:long}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<ReportRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ReportRecord>> Update(string tenantId, long reportId, ReportInput input, CancellationToken cancellationToken)
    {
        return Ok(await reports.UpdateAsync(reportId, input, cancellationToken));
    }

    /// <summary>Replaces a report's settings: its default profile, subtotal position, default filter, settings and pivot.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The new settings.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored report.</returns>
    /// <response code="200">The stored report.</response>
    /// <response code="400">The input breaks a field rule, such as a default profile the report does not have; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the settings would break its rules; nothing was written.</response>
    [HttpPut("{reportId:long}/settings")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<ReportRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<ReportRecord>> UpdateSettings(string tenantId, long reportId, ReportSettingsInput input, CancellationToken cancellationToken)
    {
        var violations = await reports.UpdateSettingsAsync(reportId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, reportId, violations);
        return Ok(await reports.GetAsync(reportId, cancellationToken));
    }

    /// <summary>
    /// Enables or disables a report. Enabling checks it against every report rule and keeps it disabled on any violation; disabling
    /// always succeeds. Either way the semantic revision advances, because the engine sees enabled reports only.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">Whether the report is enabled.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored report.</returns>
    /// <response code="200">The stored report.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report breaks its rules, so it stays disabled; the violations say what to fix.</response>
    [HttpPut("{reportId:long}/status")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<ReportRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<ReportRecord>> ChangeStatus(string tenantId, long reportId, ReportStatusInput input, CancellationToken cancellationToken)
    {
        var violations = await statuses.SetAsync(reportId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.NotEnabled(HttpContext, reportId, violations);
        return Ok(await reports.GetAsync(reportId, cancellationToken));
    }

    /// <summary>Checks a report against every report rule, enabled or not, and changes nothing.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The violations the rules found.</returns>
    /// <response code="200">The check ran; <c>violations</c> lists what the rules found and is empty when the report could be enabled.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost("{reportId:long}/validation")]
    [ProducesResponseType<ReportCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportCheck>> Check(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(new ReportCheck { Violations = await statuses.CheckAsync(reportId, cancellationToken) });
    }

    /// <summary>
    /// Deletes a report with everything it owns: its content, its sources with their bridges, and its report dimension. Nothing
    /// outside the report names them, so nothing blocks the delete.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The report and everything it owns are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpDelete("{reportId:long}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        await reports.DeleteAsync(reportId, cancellationToken);
        return NoContent();
    }
}
