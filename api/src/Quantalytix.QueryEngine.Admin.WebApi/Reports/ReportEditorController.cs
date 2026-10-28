using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>Reads a report as its editor shows it, and replaces its row tree.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports/{reportId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class ReportEditorController(ReportEditorReader reader, ReportRowService rows) : ControllerBase
{
    /// <summary>
    /// Reads a report's editor view: its rows with the items each bridge source maps to them, and its sources with their variables.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The editor view.</returns>
    /// <response code="200">The editor view.</response>
    [HttpGet("editor")]
    [ProducesResponseType<EditorView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EditorView>> Read(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await reader.ReadAsync(reportId, cancellationToken));
    }

    /// <summary>
    /// Replaces a report's row tree: members of its report dimension, with their options, and its overlay rows. A removed row takes
    /// the bridge entries, variable scopes and member option that name it with it.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The complete row tree.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The editor view afterwards.</returns>
    /// <response code="200">The editor view afterwards.</response>
    /// <response code="400">The tree breaks a field rule or its shape, such as two top-level member rows; errors are keyed by camelCase JSON path.</response>
    /// <response code="422">The report is enabled and the tree would break its rules; nothing was written.</response>
    [HttpPut("rows")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<EditorView>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<EditorView>> ReplaceRows(string tenantId, long reportId, ReportRowsInput input, CancellationToken cancellationToken)
    {
        var violations = await rows.ReplaceAsync(reportId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(reportId, violations);
        return Ok(await reader.ReadAsync(reportId, cancellationToken));
    }
}
