using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Mappings;

/// <summary>
/// Mapping versions (blueprint 13, OI-14): the content of a live or frozen version, its rule check, the freeze of the live version,
/// and deleting a frozen one with its check.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/mapping-versions/{mappingVersionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class MappingVersionsController(MappingQueries queries, MappingCommands commands) : ControllerBase
{
    /// <summary>Reads a version with its entries, coverage and rule findings.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version content.</returns>
    /// <response code="200">The version, its entries, coverage and rule findings.</response>
    [HttpGet]
    [ProducesResponseType<MappingVersionContent>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MappingVersionContent>> Get(string tenantId, long mappingVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ContentAsync(mappingVersionId, cancellationToken));
    }

    /// <summary>Deletes a frozen version that no historical assumption set pins.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The version is live, or a historical assumption set pins it (see <c>violations</c>).</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long mappingVersionId, CancellationToken cancellationToken)
    {
        return await commands.DeleteVersionAsync(mappingVersionId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Runs every mapping version rule on the version and writes nothing (OI-30).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The rule report.</returns>
    /// <response code="200">The rule report; codes are <c>MappingViolationCode</c> values.</response>
    /// <response code="400">The route does not bind.</response>
    [HttpPost("check")]
    [ProducesResponseType<ValidationReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<ValidationReport>> Check(string tenantId, long mappingVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.CheckAsync(mappingVersionId, cancellationToken));
    }

    /// <summary>
    /// Freezes the live version: when every rule passes, clones it to a new live version, retires it with the note, repoints the
    /// active assumption set and advances the semantic revision.
    /// </summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingVersionId">The live version.</param>
    /// <param name="input">The freeze note.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The report and the new live version.</returns>
    /// <response code="200">Frozen; <c>live</c> is the new live version.</response>
    /// <response code="400">The note is too long.</response>
    /// <response code="409">The version is not live.</response>
    /// <response code="422">A rule fails; nothing changed.</response>
    [HttpPost("freeze")]
    [ProducesResponseType<MappingFreezeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<MappingFreezeResult>> Freeze(string tenantId, long mappingVersionId, FreezeInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.FreezeAsync(mappingVersionId, input.Note, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>Checks whether the version can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">Whether the delete is allowed, and why not.</response>
    [HttpGet("delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckDelete(string tenantId, long mappingVersionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.VersionDeleteCheckAsync(mappingVersionId, cancellationToken));
    }
}
