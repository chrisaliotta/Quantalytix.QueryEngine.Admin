using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>Lists and replaces the columns of reports: the model metrics a report returns, in order.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports/{reportId:long}/columns")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportColumnsController(ReportColumnService columns) : ControllerBase
{
    /// <summary>Lists a report's columns in display order.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The columns.</returns>
    /// <response code="200">The columns in display order.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportColumnRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportColumnRecord>>> List(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await columns.ListAsync(reportId, cancellationToken));
    }

    /// <summary>
    /// Replaces a report's columns with the given ordered list; a column that keeps its ID keeps it, so formula rows that name it
    /// stay valid.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The complete columns.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored columns.</returns>
    /// <response code="200">The columns in display order.</response>
    /// <response code="400">A column key is reserved or repeated, a metric is not in the report's model, or a formula row still names a removed column; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the columns would break its rules, such as a profile without a pick for a newly read variable; nothing was written.</response>
    [HttpPut]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<IReadOnlyList<ReportColumnRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ReportColumnRecord>>> Replace(
        string tenantId,
        long reportId,
        ReportColumnsInput input,
        CancellationToken cancellationToken)
    {
        var violations = await columns.ReplaceAsync(reportId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, reportId, violations);
        return Ok(await columns.ListAsync(reportId, cancellationToken));
    }
}
