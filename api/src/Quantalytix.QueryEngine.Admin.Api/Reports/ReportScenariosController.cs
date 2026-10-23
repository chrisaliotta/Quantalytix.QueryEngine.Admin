using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>
/// Lists and replaces the scenarios of reports: the roles whose members are alternatives a run reads one at a time, such as plan
/// versions or interest rate shocks. A run or comparison names one member of each scenario its sources read, by the role's key.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports/{reportId:long}/scenarios")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportScenariosController(ReportScenarioService scenarios) : ControllerBase
{
    /// <summary>Lists the roles that are a report's scenarios.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The scenario roles.</returns>
    /// <response code="200">The scenario roles by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DimensionRoleRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DimensionRoleRecord>>> List(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await scenarios.ListAsync(reportId, cancellationToken));
    }

    /// <summary>
    /// Replaces a report's scenarios; whether a calculation dimension of the report reads a scenario's role is checked by the report
    /// rules, when the report is enabled and on every write to it while it is.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The complete scenarios.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored scenario roles.</returns>
    /// <response code="200">The scenario roles by key, ignoring case.</response>
    /// <response code="400">A role does not exist, is named twice, or shares its dimension with another role, since a scenario's dimension has exactly one role; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the scenarios would break its rules, such as a scenario on the role of a calculation dimension; nothing was written.</response>
    [HttpPut]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<IReadOnlyList<DimensionRoleRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<DimensionRoleRecord>>> Replace(
        string tenantId,
        long reportId,
        ReportScenariosInput input,
        CancellationToken cancellationToken)
    {
        var violations = await scenarios.ReplaceAsync(reportId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, reportId, violations);
        return Ok(await scenarios.ListAsync(reportId, cancellationToken));
    }
}
