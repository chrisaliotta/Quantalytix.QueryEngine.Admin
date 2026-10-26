using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>
/// Creates, edits and deletes a report's sources: datasets whose rows reach the report's rows through the source's own bridge or a
/// field of row keys. Edits the variables each source supplies, and the rules a bridge source adds into each row.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports/{reportId:long}/sources")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportSourcesController(
    ReportSourceService sources,
    ReportSourceVariableService variables,
    SourceItemService items,
    SourceCoverageReader coverage,
    ReportEditorReader reader) : ControllerBase
{
    /// <summary>
    /// Creates a source. A bridge source gets its own bridge from its role's dimension into the report dimension, published at
    /// once, empty or with a copy of a sibling's entries; a direct source reads row keys from its field.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The new source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The editor view afterwards.</returns>
    /// <response code="200">The editor view afterwards.</response>
    /// <response code="400">The input breaks a field rule, such as a dataset the active release does not pin; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the source would break its rules; nothing was written.</response>
    [HttpPost]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<EditorView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<EditorView>> Create(string tenantId, long reportId, NewReportSourceInput input, CancellationToken cancellationToken)
    {
        var violations = await sources.CreateAsync(reportId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, reportId, violations);
        return Ok(await reader.ReadAsync(reportId, cancellationToken));
    }

    /// <summary>Edits a source's label, which a bridge source's bridge takes too, and its time field.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="source">The report and its source.</param>
    /// <param name="input">The source's new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The editor view afterwards.</returns>
    /// <response code="200">The editor view afterwards.</response>
    /// <response code="400">The source is not the report's, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the edit would break its rules; nothing was written.</response>
    [HttpPut("{sourceId:long}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<EditorView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<EditorView>> Update(
        string tenantId,
        [FromRoute] ReportSourceReference source,
        ReportSourceInput input,
        CancellationToken cancellationToken)
    {
        var violations = await sources.UpdateAsync(source.ReportId, source.SourceId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, source.ReportId, violations);
        return Ok(await reader.ReadAsync(source.ReportId, cancellationToken));
    }

    /// <summary>Deletes a source with its variables, the profile picks that name it, and a bridge source's bridge.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="source">The report and its source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The editor view afterwards.</returns>
    /// <response code="200">The editor view afterwards.</response>
    /// <response code="400">The source is not the report's; the error is keyed <c>sourceId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and losing the source would break its rules, such as a profile left without a pick; nothing was deleted.</response>
    [HttpDelete("{sourceId:long}")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<EditorView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<EditorView>> Delete(string tenantId, [FromRoute] ReportSourceReference source, CancellationToken cancellationToken)
    {
        var violations = await sources.DeleteAsync(source.ReportId, source.SourceId, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, source.ReportId, violations);
        return Ok(await reader.ReadAsync(source.ReportId, cancellationToken));
    }

    /// <summary>
    /// Replaces the variables a source supplies, each a dataset measure with an optional filter. Every profile that picks no
    /// source for one of them picks this source, and a report without profiles gets profile <c>default</c>, which it runs under.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="source">The report and its source.</param>
    /// <param name="input">The source's complete variable list.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The editor view afterwards.</returns>
    /// <response code="200">The editor view afterwards.</response>
    /// <response code="400">The source is not the report's, or the input breaks a field rule, such as a disabled measure; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the list would break its rules; nothing was written.</response>
    [HttpPut("{sourceId:long}/variables")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<EditorView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<EditorView>> ReplaceVariables(
        string tenantId,
        [FromRoute] ReportSourceReference source,
        SourceVariablesInput input,
        CancellationToken cancellationToken)
    {
        var violations = await variables.ReplaceAsync(source.ReportId, source.SourceId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, source.ReportId, violations);
        return Ok(await reader.ReadAsync(source.ReportId, cancellationToken));
    }

    /// <summary>
    /// Replaces the rules a bridge source adds into one data row: items, or every item under a member, each with a factor. The
    /// source's other rows keep their rules, so an item may add into several rows. While the report is enabled, the write is
    /// checked against the report rules first, such as that a factor other than 1 only scales measures that apply bridge factors.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="source">The report and its bridge source.</param>
    /// <param name="input">The row and its complete rule list.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The editor view afterwards.</returns>
    /// <response code="200">The editor view afterwards.</response>
    /// <response code="400">The source is not the report's or is direct, or the input breaks a field rule, such as an item that is no member, or an item taken alone that is no assignable member; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The report is enabled and the rules would break a report rule; nothing was written.</response>
    [HttpPut("{sourceId:long}/items")]
    [AdvancesReportRevision("reportId")]
    [ProducesResponseType<EditorView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<EditorView>> ReplaceItems(
        string tenantId,
        [FromRoute] ReportSourceReference source,
        SourceItemsInput input,
        CancellationToken cancellationToken)
    {
        var violations = await items.ReplaceAsync(source.ReportId, source.SourceId, input, cancellationToken);
        if (violations.Count > 0) return ReportWriteResults.Rejected(HttpContext, source.ReportId, violations);
        return Ok(await reader.ReadAsync(source.ReportId, cancellationToken));
    }

    /// <summary>Lists the assignable items of a bridge source's item dimension that no rule of its bridge reaches.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="source">The report and its bridge source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The unmapped item keys.</returns>
    /// <response code="200">The unmapped item keys, in ordinal order.</response>
    /// <response code="400">The source is not the report's; the error is keyed <c>sourceId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{sourceId:long}/unmapped")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<string>>> Unmapped(string tenantId, [FromRoute] ReportSourceReference source, CancellationToken cancellationToken)
    {
        return Ok(await coverage.UnmappedAsync(source.SourceId, cancellationToken));
    }
}
