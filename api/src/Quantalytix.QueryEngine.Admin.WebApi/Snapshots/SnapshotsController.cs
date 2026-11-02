using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Snapshots;

/// <summary>
/// Snapshots (blueprint 14): the live snapshot and the frozen ones, a snapshot with the version it holds of each identity, the
/// comparison of two snapshots, and capturing live into a frozen snapshot, restoring a frozen one into live, relabelling and deleting
/// it. A request names a frozen snapshot to rerun the versions it holds.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/snapshots")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class SnapshotsController(SnapshotQueries queries, SnapshotCommands commands, CallerFactory callers) : ControllerBase
{
    /// <summary>Lists the live snapshot and the frozen ones with their version counts.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The snapshots.</returns>
    /// <response code="200">The live snapshot and every frozen one, newest capture first.</response>
    [HttpGet]
    [ProducesResponseType<SnapshotList>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SnapshotList>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ListAsync(cancellationToken));
    }

    /// <summary>Captures every live version into a new frozen snapshot; advances the semantic revision.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The key and labels.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new snapshot.</returns>
    /// <response code="201">The new frozen snapshot.</response>
    /// <response code="400">The key is malformed or a label is missing or too long.</response>
    /// <response code="409">The key is taken, or metadata was saved during the capture, which then captured nothing.</response>
    [HttpPost]
    [ProducesResponseType<Snapshot>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<Snapshot>> Capture(string tenantId, CaptureSnapshotInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.CaptureAsync(input, callers.Create(User).Subject, cancellationToken);
        if (!outcome.Succeeded) return Problems.From(outcome.Refusal).ToResult();
        return CreatedAtAction(nameof(Get), new { tenantId, snapshotId = outcome.Value.Id }, outcome.Value);
    }

    /// <summary>Counts what a capture would copy now.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The counts.</returns>
    /// <response code="200">The live versions of each kind, which a capture copies.</response>
    [HttpGet("capture-preview")]
    [ProducesResponseType<SnapshotCounts>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SnapshotCounts>> CapturePreview(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await queries.CapturePreviewAsync(cancellationToken));
    }

    /// <summary>Compares the identities and versions two snapshots hold.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="from">The snapshot compared from.</param>
    /// <param name="to">The snapshot compared to.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The comparison.</returns>
    /// <response code="200">What <c>to</c> holds that <c>from</c> does not, the reverse, and what both hold.</response>
    /// <response code="404"><c>from</c> or <c>to</c> names no snapshot.</response>
    [HttpGet("compare")]
    [ProducesResponseType<SnapshotComparison>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status404NotFound, ProblemWriter.ContentType)]
    public async Task<ActionResult<SnapshotComparison>> Compare(
        string tenantId,
        [FromQuery, BindRequired] long from,
        [FromQuery, BindRequired] long to,
        CancellationToken cancellationToken)
    {
        return await queries.CompareAsync(from, to, cancellationToken) is { } comparison ? Ok(comparison) : Problems.OfStatus(StatusCodes.Status404NotFound).ToResult();
    }

    /// <summary>Reads a snapshot with the version it holds of each identity and its restores.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The snapshot.</returns>
    /// <response code="200">The snapshot.</response>
    [HttpGet("{snapshotId:long}")]
    [ProducesResponseType<SnapshotDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SnapshotDetail>> Get(string tenantId, long snapshotId, CancellationToken cancellationToken)
    {
        return Ok(await queries.GetAsync(snapshotId, cancellationToken));
    }

    /// <summary>Changes a frozen snapshot's display name and description; the key and the versions never change.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="snapshotId">The frozen snapshot.</param>
    /// <param name="input">The new labels.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The relabelled snapshot.</returns>
    /// <response code="200">The relabelled snapshot.</response>
    /// <response code="400">A label is missing or too long.</response>
    /// <response code="409">The live snapshot.</response>
    [HttpPut("{snapshotId:long}")]
    [ProducesResponseType<Snapshot>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<Snapshot>> Relabel(string tenantId, long snapshotId, RelabelSnapshotInput input, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.RelabelAsync(snapshotId, input, cancellationToken));
    }

    /// <summary>Deletes a frozen snapshot with every version it holds; advances the semantic revision.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="snapshotId">The frozen snapshot.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The live snapshot.</response>
    [HttpDelete("{snapshotId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long snapshotId, CancellationToken cancellationToken)
    {
        return await commands.DeleteAsync(snapshotId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Makes live equal a frozen snapshot, once every version it holds passes its rules; advances the semantic revision.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="snapshotId">The frozen snapshot.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The tenant state the restore produced.</returns>
    /// <response code="200">Restored; the semantic revision advanced and the restore was logged.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The live snapshot.</response>
    /// <response code="422">A version breaks a rule against the database as it is now; nothing was restored.</response>
    [HttpPost("{snapshotId:long}/restore")]
    [ProducesResponseType<TenantInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<TenantInfo>> Restore(string tenantId, long snapshotId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.RestoreAsync(snapshotId, callers.Create(User).Subject, cancellationToken));
    }

    /// <summary>200 with the value, or the refusal's problem.</summary>
    private ActionResult<T> OkOrProblem<T>(Outcome<T> outcome) where T : class
    {
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }
}
