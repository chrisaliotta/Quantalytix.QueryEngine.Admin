using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Dimensions;

/// <summary>
/// Dimension versions (blueprint 12, OI-14): the content of a live or frozen version, its rule check, the freeze of the live
/// version, deleting a frozen one, and the member and level delete checks. A report dimension's versions answer 404 to reads and
/// 409 to writes (M8).
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/dimension-versions/{versionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class DimensionVersionsController(DimensionQueries queries, DimensionCommands commands) : ControllerBase
{
    /// <summary>Reads a version with its levels, members and bands.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version content.</returns>
    /// <response code="200">The version with its levels, members and bands.</response>
    [HttpGet]
    [ProducesResponseType<DimensionVersionContent>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DimensionVersionContent>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.ContentAsync(versionId, cancellationToken));
    }

    /// <summary>Deletes a version that is not live and that no historical assumption set pins.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The live version, pinned by a historical assumption set (see <c>violations</c>), or owned by a report.</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return await commands.DeleteVersionAsync(versionId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Runs every dimension rule on the version and changes nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The rule report.</returns>
    /// <response code="200">The rule report; codes are <c>DimensionViolationCode</c> values.</response>
    /// <response code="400">The route does not bind.</response>
    [HttpPost("check")]
    [ProducesResponseType<ValidationReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<ValidationReport>> Check(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.CheckAsync(versionId, cancellationToken));
    }

    /// <summary>
    /// Freezes the live version: when every rule passes, clones it to a new live version, retires it with the note, repoints the
    /// active assumption set and advances the semantic revision.
    /// </summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The live version.</param>
    /// <param name="input">The freeze note.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The report and the new live version.</returns>
    /// <response code="200">Frozen; <c>live</c> is the new live version.</response>
    /// <response code="400">The note is too long.</response>
    /// <response code="409">Not the live version, locked by its profile, or owned by a report.</response>
    /// <response code="422">A rule fails; nothing changed.</response>
    [HttpPost("freeze")]
    [ProducesResponseType<DimensionFreezeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<DimensionFreezeResult>> Freeze(string tenantId, long versionId, FreezeInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.FreezeAsync(versionId, input.Note, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>Checks whether the version can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check.</response>
    [HttpGet("delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckDelete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.VersionDeleteCheckAsync(versionId, cancellationToken));
    }

    /// <summary>Checks whether a member can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The member's version.</param>
    /// <param name="memberId">The member.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check.</response>
    [HttpGet("members/{memberId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckMemberDelete(string tenantId, long versionId, long memberId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.MemberDeleteCheckAsync(versionId, memberId, cancellationToken));
    }

    /// <summary>Checks whether a level can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The level's version.</param>
    /// <param name="levelId">The level.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check.</response>
    [HttpGet("levels/{levelId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckLevelDelete(string tenantId, long versionId, long levelId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.LevelDeleteCheckAsync(versionId, levelId, cancellationToken));
    }

    /// <summary>200 with the value, or 404 for a report dimension's.</summary>
    private ActionResult<T> OkOrNotFound<T>(T? value) where T : class
    {
        return value is null ? Problems.OfStatus(StatusCodes.Status404NotFound).ToResult() : Ok(value);
    }
}
