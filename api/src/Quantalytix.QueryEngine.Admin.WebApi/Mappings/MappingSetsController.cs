using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Mappings;

/// <summary>
/// Mapping sets (blueprint 13): the list, create with an empty live version 1, the detail, delete and its check, and the mapping set
/// change set over the identity and the live version's entries.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/mapping-sets")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class MappingSetsController(MappingQueries queries, MappingCommands commands, MappingSetChanges changes) : ControllerBase
{
    /// <summary>Lists every mapping set, or those that target one dimension.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="query">The optional dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The list items by key.</returns>
    /// <response code="200">The mapping sets, each with its dimension, version summaries and bindings.</response>
    /// <response code="400">The query does not bind.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MappingSetListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<IReadOnlyList<MappingSetListItem>>> List(string tenantId, [FromQuery] MappingSetListQuery query, CancellationToken cancellationToken)
    {
        return Ok(await queries.ListAsync(query, cancellationToken));
    }

    /// <summary>Creates a mapping set with an empty live version 1 (OI-27).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The new mapping set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The mapping set and its version.</returns>
    /// <response code="201">Created, with its live version 1.</response>
    /// <response code="400">The input breaks a field rule, or the dimension does not exist.</response>
    /// <response code="409">The key is taken, or the dimension is not an analytical categorical or hierarchical dimension (B1).</response>
    [HttpPost]
    [ProducesResponseType<MappingSetCreated>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<MappingSetCreated>> Create(string tenantId, CreateMappingSetInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.CreateAsync(input, cancellationToken);
        return outcome.Succeeded ? StatusCode(StatusCodes.Status201Created, outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>Reads a mapping set with its bindings and assumption set pins.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The detail.</returns>
    /// <response code="200">The detail read model.</response>
    [HttpGet("{mappingSetId:long}")]
    [ProducesResponseType<MappingSetDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MappingSetDetail>> Get(string tenantId, long mappingSetId, CancellationToken cancellationToken)
    {
        return Ok(await queries.DetailAsync(mappingSetId, cancellationToken));
    }

    /// <summary>Deletes a mapping set and every version when no binding uses it and no historical set pins it (B12).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Blocked; <c>violations</c> lists the same reasons as the delete check.</response>
    [HttpDelete("{mappingSetId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long mappingSetId, CancellationToken cancellationToken)
    {
        return await commands.DeleteAsync(mappingSetId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Checks whether a mapping set can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">Whether the delete is allowed, and why not.</response>
    [HttpGet("{mappingSetId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckDelete(string tenantId, long mappingSetId, CancellationToken cancellationToken)
    {
        return Ok(await queries.SetDeleteCheckAsync(mappingSetId, cancellationToken));
    }

    /// <summary>Previews a mapping set change set: applies the operations in memory and writes nothing; broken entries come back as findings.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The pending state and its findings.</returns>
    /// <response code="200">The operations apply.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">An operation targets a frozen version or an entry that is gone.</response>
    /// <response code="422">The result would break a rule.</response>
    [HttpPost("{mappingSetId:long}/changes/preview")]
    [ProducesResponseType<ChangeSetPreview<MappingSetEditState>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetPreview<MappingSetEditState>>> Preview(
        string tenantId,
        long mappingSetId,
        ChangeSetRequest<MappingOperation> request,
        CancellationToken cancellationToken)
    {
        var outcome = await changes.PreviewAsync(mappingSetId, request, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>Commits a mapping set change set; refused while any finding remains (B11), and advances the semantic revision once.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored state and the real id of each temporary id.</returns>
    /// <response code="200">Every operation applied and was written.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">An operation targets a frozen version or an entry that is gone.</response>
    /// <response code="422">The live version would break a rule; see <c>violations</c>. Nothing was written.</response>
    [HttpPost("{mappingSetId:long}/changes")]
    [ProducesResponseType<ChangeSetCommit<MappingSetEditState>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetCommit<MappingSetEditState>>> Commit(
        string tenantId,
        long mappingSetId,
        ChangeSetRequest<MappingOperation> request,
        CancellationToken cancellationToken)
    {
        var outcome = await changes.CommitAsync(mappingSetId, request, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }
}
