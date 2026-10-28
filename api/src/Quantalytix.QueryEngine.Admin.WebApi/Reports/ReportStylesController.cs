using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>
/// Lists, writes and deletes the styles of reports: how the rows that name a style show, and every row of its default role that
/// names none. Each is keyed by its key and written whole.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports/{reportId:long}/styles")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class ReportStylesController(ReportStyleService styles, ReportChanges changes) : ControllerBase
{
    /// <summary>Lists a report's styles.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The styles by key.</returns>
    /// <response code="200">The styles by key.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportStyleRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportStyleRecord>>> List(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await styles.ListAsync(reportId, cancellationToken));
    }

    /// <summary>Sets a style of a report, creating it under the key or replacing the one stored.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="style">The report and the style's key.</param>
    /// <param name="input">The style.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored style.</returns>
    /// <response code="200">The stored style.</response>
    /// <response code="400">The key or the input breaks a field rule, such as a color that is not #RRGGBB; errors are keyed by camelCase JSON path.</response>
    /// <response code="422">The report is enabled and the style would break its rules, such as a second default style of a role; nothing was written.</response>
    [HttpPut("{styleKey}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<ReportStyleRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<ReportStyleRecord>> Replace(
        string tenantId,
        [FromRoute] ReportStyleReference style,
        ReportStyleInput input,
        CancellationToken cancellationToken)
    {
        var stored = ReportStyleService.Compose(style.ReportId, style.StyleKey, input);
        var violations = await changes.ApplyAsync(
            style.ReportId, report => ReportStyleService.Put(report, stored), () => styles.ReplaceAsync(stored, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(style.ReportId, violations);
        return Ok(stored);
    }

    /// <summary>Deletes a style of a report, unless a row of the report shows it.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="styleKey">The style's key.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 or 422 when nothing was deleted.</returns>
    /// <response code="204">The style is deleted, or the report had none by the key.</response>
    /// <response code="409">Nothing was deleted: rows of the report show the style; the detail names them.</response>
    /// <response code="422">The report is enabled and losing the style would break its rules, such as a row that shows it; nothing was deleted.</response>
    [HttpDelete("{styleKey}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long reportId, string styleKey, CancellationToken cancellationToken)
    {
        string? conflict = null;
        var violations = await changes.ApplyAsync(
            reportId,
            report => ReportStyleService.Without(report, styleKey),
            async () => conflict = await styles.DeleteAsync(reportId, styleKey, cancellationToken),
            cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(reportId, violations);
        if (conflict is not null) return Problems.DeleteBlocked(conflict).ToResult();
        return NoContent();
    }
}
