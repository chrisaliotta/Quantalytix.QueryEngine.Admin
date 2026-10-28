using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>
/// The report library and editor (blueprint 16): the list, create, the report document, its diagnostics and a source's unmapped
/// items, the report change set, enable and delete. An enabled report's change set passes every report rule or is refused (P2).
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/reports")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class ReportsController(ReportQueries queries, ReportCommands commands, ReportRemover remover, ReportChanges changes) : ControllerBase
{
    /// <summary>Lists the tenant's reports.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The summaries by key.</returns>
    /// <response code="200">Every report, enabled or not.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportSummary>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ListAsync(cancellationToken));
    }

    /// <summary>Creates a disabled report with its report dimension and, when copying, its content.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The new report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The report's summary.</returns>
    /// <response code="201">The report was created.</response>
    /// <response code="400">The input breaks a field rule.</response>
    /// <response code="409">The report key is taken.</response>
    [HttpPost]
    [ProducesResponseType<ReportSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<ReportSummary>> Create(string tenantId, ReportCreateInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.CreateAsync(input, cancellationToken);
        return outcome.Succeeded ? StatusCode(StatusCodes.Status201Created, outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>Reads a report's whole document.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The document.</returns>
    /// <response code="200">The report as stored.</response>
    [HttpGet("{reportId:long}")]
    [ProducesResponseType<ReportDocument>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportDocument>> Get(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await queries.DocumentAsync(reportId, cancellationToken));
    }

    /// <summary>Deletes a report with its report dimension, report bridges and content; never blocked (P12).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The report and everything it owns were deleted.</response>
    /// <response code="400">The route does not bind.</response>
    [HttpDelete("{reportId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        await remover.DeleteAsync(reportId, cancellationToken);
        return NoContent();
    }

    /// <summary>Checks the stored report against the report rules, with coverage and profile resolution.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The diagnostics.</returns>
    /// <response code="200">The diagnostics of the stored report.</response>
    [HttpGet("{reportId:long}/diagnostics")]
    [ProducesResponseType<ReportDiagnostics>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportDiagnostics>> Diagnostics(string tenantId, long reportId, CancellationToken cancellationToken)
    {
        return Ok(await queries.DiagnosticsAsync(reportId, cancellationToken));
    }

    /// <summary>Lists the items a source reads that reach no row and are not left out.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="sourceKey">The source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The item keys.</returns>
    /// <response code="200">Item keys.</response>
    /// <response code="404">No such report, or the report has no source with that key.</response>
    [HttpGet("{reportId:long}/sources/{sourceKey}/unmapped")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status404NotFound, ProblemWriter.ContentType)]
    public async Task<ActionResult<IReadOnlyList<string>>> Unmapped(string tenantId, long reportId, string sourceKey, CancellationToken cancellationToken)
    {
        return await queries.UnmappedAsync(reportId, sourceKey, cancellationToken) is { } items
            ? Ok(items)
            : Problems.OfStatus(StatusCodes.Status404NotFound).ToResult();
    }

    /// <summary>Previews a report change set: applies the operations in memory, diagnoses the result, and writes nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The pending document with its findings and diagnostics.</returns>
    /// <response code="200">Every operation applies.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">An operation names a row, source, profile, dimension or style the report does not have.</response>
    /// <response code="422">The report is enabled and the result fails the report gate, or a cascade is refused.</response>
    [HttpPost("{reportId:long}/changes/preview")]
    [ProducesResponseType<ReportChangeSetPreview>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ReportChangeSetPreview>> Preview(
        string tenantId,
        long reportId,
        ChangeSetRequest<ReportOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.PreviewAsync(reportId, request, cancellationToken));
    }

    /// <summary>Commits a report change set: re-applies the operations to the report as stored now and writes them as one batch.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored document.</returns>
    /// <response code="200">Every operation applied and was written.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">An operation names a row, source, profile, dimension or style the report does not have.</response>
    /// <response code="422">The report is enabled and the result fails the report gate, or a cascade is refused; nothing was written.</response>
    [HttpPost("{reportId:long}/changes")]
    [ProducesResponseType<ChangeSetCommit<ReportDocument>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetCommit<ReportDocument>>> Commit(
        string tenantId,
        long reportId,
        ChangeSetRequest<ReportOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.CommitAsync(reportId, request, cancellationToken));
    }

    /// <summary>Enables or disables a report; enabling runs every report rule on the stored report.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="input">Whether it is enabled.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The report's summary.</returns>
    /// <response code="200">The report's new state.</response>
    /// <response code="400">The input breaks a field rule.</response>
    /// <response code="422">A report rule fails; the report stays disabled.</response>
    [HttpPut("{reportId:long}/enabled")]
    [ProducesResponseType<ReportSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ReportSummary>> SetEnabled(string tenantId, long reportId, ReportEnabledInput input, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.SetEnabledAsync(reportId, input, cancellationToken));
    }

    /// <summary>200 with the value, or the refusal's problem.</summary>
    private ActionResult<T> OkOrProblem<T>(Outcome<T> outcome) where T : class
    {
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }
}
