using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists and edits the calculation dimensions of reports: the dimensions a run can break out or filter by.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports/{reportId:long}/calculation-dimensions")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class CalculationDimensionsController(
    IReportContent<CalculationDimensionRecord, CalculationDimensionInput> dimensions,
    ReportChanges changes) : ControllerBase
{
    /// <summary>Lists a report's calculation dimensions.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The calculation dimensions by key.</returns>
    /// <response code="200">The calculation dimensions by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CalculationDimensionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalculationDimensionRecord>>> List(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await dimensions.ListAsync(reportId, cancellationToken));
    }

    /// <summary>Adds a calculation dimension to a report.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The new calculation dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created calculation dimension.</returns>
    /// <response code="200">The created calculation dimension.</response>
    /// <response code="400">The input breaks a field rule, such as a taken key; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the calculation dimension would break its rules; nothing was written.</response>
    [HttpPost]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<CalculationDimensionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CalculationDimensionRecord>> Create(string tenantId, long reportId, CalculationDimensionInput input, CancellationToken cancellationToken)
    {
        var created = dimensions.Compose(reportId, null, input);
        var violations = await changes.ApplyAsync(reportId, report => dimensions.Put(report, created), () => dimensions.InsertAsync(created, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, reportId, violations);
        return Ok(created);
    }

    /// <summary>Replaces a calculation dimension of a report, keeping its ID.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="dimension">The report and its calculation dimension.</param>
    /// <param name="input">The calculation dimension's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored calculation dimension.</returns>
    /// <response code="200">The stored calculation dimension.</response>
    /// <response code="400">The calculation dimension is not the report's, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the calculation dimension would break its rules; nothing was written.</response>
    [HttpPut("{itemId:long}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<CalculationDimensionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CalculationDimensionRecord>> Update(
        string tenantId,
        [FromRoute] ReportItemReference<CalculationDimensionRecord> dimension,
        CalculationDimensionInput input,
        CancellationToken cancellationToken)
    {
        var updated = dimensions.Compose(dimension.ReportId, dimension.ItemId, input);
        var violations = await changes.ApplyAsync(
            dimension.ReportId, report => dimensions.Put(report, updated), () => dimensions.UpdateAsync(updated, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, dimension.ReportId, violations);
        return Ok(updated);
    }

    /// <summary>Deletes a calculation dimension of a report.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="dimension">The report and its calculation dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The calculation dimension is deleted.</response>
    /// <response code="400">The calculation dimension is not the report's; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and losing the calculation dimension would break its rules, such as a default pivot on it; nothing was deleted.</response>
    [HttpDelete("{itemId:long}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] ReportItemReference<CalculationDimensionRecord> dimension, CancellationToken cancellationToken)
    {
        var violations = await changes.ApplyAsync(
            dimension.ReportId,
            report => dimensions.Without(report, dimension.ItemId),
            () => dimensions.DeleteAsync(dimension.ItemId, cancellationToken),
            cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, dimension.ReportId, violations);
        return NoContent();
    }
}
