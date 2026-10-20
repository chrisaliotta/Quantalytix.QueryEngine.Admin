using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists, creates, edits and deletes reports: the line and column layouts the engine executes over a model.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportsController(ReportService reports) : ControllerBase
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

    /// <summary>Creates a report; it has no versions until one is created.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="input">The new report's key, display name, type, availability and payload.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created report.</returns>
    /// <response code="200">The created report.</response>
    /// <response code="400">The input breaks a field rule, such as a taken key; errors are keyed by camelCase JSON path.</response>
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

    /// <summary>Edits a report's display name, type, availability and payload; a change advances the semantic revision.</summary>
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
    [ProducesResponseType<ReportRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ReportRecord>> Update(string tenantId, long reportId, ReportInput input, CancellationToken cancellationToken)
    {
        return Ok(await reports.UpdateAsync(reportId, input, cancellationToken));
    }

    /// <summary>Deletes a report whose versions are all drafts that no release pins, together with their lines and columns.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when something blocks the delete.</returns>
    /// <response code="204">The report and its drafts are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted; the detail names the non-draft versions and releases that block the delete.</response>
    [HttpDelete("{reportId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        var conflict = await reports.DeleteAsync(reportId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
