using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Bridges;

/// <summary>
/// Bridge versions (blueprint 13, OI-14): the content of a live or frozen version, its rule check, the freeze of an analytical
/// bridge's live version, and deleting a frozen one with its check. A report bridge's versions are read here and answer 409 to writes.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/bridge-versions/{bridgeVersionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class BridgeVersionsController(BridgeQueries queries, BridgeCommands commands) : ControllerBase
{
    /// <summary>Reads a version with its mappings, allocation summary and rule findings.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version content.</returns>
    /// <response code="200">The version, its mappings, the allocation summary and rule findings.</response>
    [HttpGet]
    [ProducesResponseType<BridgeVersionContent>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BridgeVersionContent>> Get(string tenantId, long bridgeVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ContentAsync(bridgeVersionId, cancellationToken));
    }

    /// <summary>Deletes a frozen version of an analytical bridge that no historical assumption set pins.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The version is live, a historical assumption set pins it, or the bridge is owned by a report.</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long bridgeVersionId, CancellationToken cancellationToken)
    {
        return await commands.DeleteVersionAsync(bridgeVersionId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Runs every bridge version rule on the version and writes nothing (OI-30).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The rule report.</returns>
    /// <response code="200">The rule report; codes are <c>BridgeViolationCode</c> values.</response>
    /// <response code="400">The route does not bind.</response>
    [HttpPost("check")]
    [ProducesResponseType<ValidationReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<ValidationReport>> Check(string tenantId, long bridgeVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.CheckAsync(bridgeVersionId, cancellationToken));
    }

    /// <summary>
    /// Freezes the live version of an analytical bridge: when every rule passes, clones it with its settings and mappings to a new live
    /// version, retires it with the note, repoints the active assumption set and advances the semantic revision.
    /// </summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeVersionId">The live version.</param>
    /// <param name="input">The freeze note.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The report and the new live version.</returns>
    /// <response code="200">Frozen; <c>live</c> is the new live version.</response>
    /// <response code="400">The note is too long.</response>
    /// <response code="409">The version is not live, or the bridge is owned by a report.</response>
    /// <response code="422">A rule fails; nothing changed.</response>
    [HttpPost("freeze")]
    [ProducesResponseType<BridgeFreezeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<BridgeFreezeResult>> Freeze(string tenantId, long bridgeVersionId, FreezeInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.FreezeAsync(bridgeVersionId, input.Note, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>Checks whether the version can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="bridgeVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">Whether the delete is allowed, and why not.</response>
    [HttpGet("delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckDelete(string tenantId, long bridgeVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.VersionDeleteCheckAsync(bridgeVersionId, cancellationToken));
    }
}
