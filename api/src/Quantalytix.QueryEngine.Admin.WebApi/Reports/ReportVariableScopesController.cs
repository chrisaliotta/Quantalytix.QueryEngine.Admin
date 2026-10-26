using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>
/// Lists and replaces the variable scopes of reports: the row whose branch each scoped variable reads. Rows inside the branch read
/// their own value, rows above it read the row's value, and other rows read nothing.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports/{reportId:long}/variable-scopes")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportVariableScopesController(ReportVariableScopeService scopes) : ControllerBase
{
    /// <summary>Lists a report's variable scopes.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The scopes.</returns>
    /// <response code="200">The scopes by variable key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportVariableScopeRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportVariableScopeRecord>>> List(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await scopes.ListAsync(reportId, cancellationToken));
    }

    /// <summary>
    /// Replaces a report's variable scopes; whether the model has each variable and the report each row is checked by the report
    /// rules, when the report is enabled and on every write to it while it is.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The complete scopes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored scopes.</returns>
    /// <response code="200">The scopes by variable key.</response>
    /// <response code="400">A variable is scoped twice, or a scope breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the scopes would break its rules; nothing was written.</response>
    [HttpPut]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<IReadOnlyList<ReportVariableScopeRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ReportVariableScopeRecord>>> Replace(
        string tenantId,
        long reportId,
        ReportVariableScopesInput input,
        CancellationToken cancellationToken)
    {
        var violations = await scopes.ReplaceAsync(reportId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, reportId, violations);
        return Ok(await scopes.ListAsync(reportId, cancellationToken));
    }
}
