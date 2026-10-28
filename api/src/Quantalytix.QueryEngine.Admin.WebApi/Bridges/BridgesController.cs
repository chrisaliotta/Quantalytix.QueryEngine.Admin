using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Bridges;

/// <summary>
/// Bridges (blueprint 13): the list of analytical and report bridges, create an analytical bridge with an empty live version 1, the
/// detail, delete and its check, and the analytical bridge change set. Report bridges are read here and answer 409 to writes (B9).
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/bridges")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class BridgesController(BridgeQueries queries, BridgeCommands commands, BridgeChanges changes) : ControllerBase
{
    /// <summary>Lists every bridge, those starting or ending at one dimension, or the one bridge of a report source.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="query">The filters.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The list items.</returns>
    /// <response code="200">Analytical bridges first, then report bridges, each ordered by key.</response>
    /// <response code="400">The filters do not bind or do not go together.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BridgeListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<IReadOnlyList<BridgeListItem>>> List(string tenantId, [FromQuery] BridgeListQuery query, CancellationToken cancellationToken)
    {
        return Ok(await queries.ListAsync(query, cancellationToken));
    }

    /// <summary>Creates an analytical bridge with an empty live version 1, <c>many_to_one</c> / <c>none</c> (OI-27).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The new bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The bridge and its version.</returns>
    /// <response code="201">Created, with its live version 1.</response>
    /// <response code="400">The input breaks a field rule, or an endpoint does not exist.</response>
    /// <response code="409">The key is taken, or the endpoints break B4 (equal, time, report dimension).</response>
    [HttpPost]
    [ProducesResponseType<BridgeCreated>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<BridgeCreated>> Create(string tenantId, CreateBridgeInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.CreateAsync(input, cancellationToken);
        return outcome.Succeeded ? StatusCode(StatusCodes.Status201Created, outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>Reads a bridge with its bindings and assumption set pins.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The detail.</returns>
    /// <response code="200">The detail read model.</response>
    [HttpGet("{bridgeId:long}")]
    [ProducesResponseType<BridgeDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BridgeDetail>> Get(string tenantId, long bridgeId, CancellationToken cancellationToken)
    {
        return Ok(await queries.DetailAsync(bridgeId, cancellationToken));
    }

    /// <summary>Deletes an analytical bridge and every version when no binding uses it and no historical set pins it (B12).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Blocked (<c>violations</c> as in the delete check), or owned by a report.</response>
    [HttpDelete("{bridgeId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long bridgeId, CancellationToken cancellationToken)
    {
        return await commands.DeleteAsync(bridgeId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Checks whether a bridge can be deleted; a report bridge never can here.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">Whether the delete is allowed, and why not.</response>
    [HttpGet("{bridgeId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckDelete(string tenantId, long bridgeId, CancellationToken cancellationToken)
    {
        return Ok(await queries.BridgeDeleteCheckAsync(bridgeId, cancellationToken));
    }

    /// <summary>Previews an analytical bridge change set: applies the operations in memory and writes nothing; broken mappings come back as findings.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The pending state and its findings.</returns>
    /// <response code="200">The operations apply.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">The bridge is owned by a report, or an operation targets a frozen version or a mapping that is gone.</response>
    /// <response code="422">The result would break a rule.</response>
    [HttpPost("{bridgeId:long}/changes/preview")]
    [ProducesResponseType<ChangeSetPreview<BridgeEditState>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetPreview<BridgeEditState>>> Preview(
        string tenantId,
        long bridgeId,
        ChangeSetRequest<BridgeOperation> request,
        CancellationToken cancellationToken)
    {
        var outcome = await changes.PreviewAsync(bridgeId, request, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>Commits an analytical bridge change set; refused while any finding remains (B11), and advances the semantic revision once.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored state and the real id of each temporary id.</returns>
    /// <response code="200">Every operation applied and was written.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">The bridge is owned by a report, or an operation targets a frozen version or a mapping that is gone.</response>
    /// <response code="422">The live version would break a rule; see <c>violations</c>. Nothing was written.</response>
    [HttpPost("{bridgeId:long}/changes")]
    [ProducesResponseType<ChangeSetCommit<BridgeEditState>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetCommit<BridgeEditState>>> Commit(
        string tenantId,
        long bridgeId,
        ChangeSetRequest<BridgeOperation> request,
        CancellationToken cancellationToken)
    {
        var outcome = await changes.CommitAsync(bridgeId, request, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }
}
