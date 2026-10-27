using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Dimensions;

/// <summary>
/// The dimension manager (blueprint 12): lists, profiles, create and define, the detail, the dimension change set, delete, and the
/// dimension and role delete checks. Report dimensions are not listed and answer 404 to reads and 409 to writes (M8).
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class DimensionsController(DimensionQueries queries, DimensionCommands commands, DimensionChanges changes) : ControllerBase
{
    /// <summary>Lists the analytical dimensions of one origin.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="query">The origin.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The list items by display name.</returns>
    /// <response code="200">The list items, each with its profile, roles and version summaries.</response>
    /// <response code="400">The origin is not a known value.</response>
    [HttpGet("dimensions")]
    [ProducesResponseType<IReadOnlyList<DimensionListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<IReadOnlyList<DimensionListItem>>> List(string tenantId, [FromQuery] DimensionListQuery query, CancellationToken cancellationToken)
    {
        return Ok(await queries.ListAsync(query.Origin, cancellationToken));
    }

    /// <summary>Creates a user dimension with its first role and a live version 1.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The new dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The dimension and its version.</returns>
    /// <response code="201">Created.</response>
    /// <response code="400">The input breaks a field rule.</response>
    /// <response code="409">The dimension key or the first role key is taken.</response>
    [HttpPost("dimensions")]
    [ProducesResponseType<DimensionCreated>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<DimensionCreated>> Create(string tenantId, CreateDimensionInput input, CancellationToken cancellationToken)
    {
        return Created(await commands.CreateAsync(input, cancellationToken));
    }

    /// <summary>Lists the system dimension profiles with the dimension defined from each.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="available">True for only the profiles the dimension manager offers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The profiles.</returns>
    /// <response code="200">Each profile with the dimension defined from it, if any.</response>
    /// <response code="400">The query does not bind.</response>
    [HttpGet("dimension-profiles")]
    [ProducesResponseType<IReadOnlyList<AvailableDimensionProfile>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<IReadOnlyList<AvailableDimensionProfile>>> Profiles(string tenantId, [FromQuery] bool available, CancellationToken cancellationToken)
    {
        return Ok(await queries.ProfilesAsync(available, cancellationToken));
    }

    /// <summary>Defines the system dimension of a profile, with its required roles and members.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="profileKey">The profile.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The dimension and its version.</returns>
    /// <response code="201">Created.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="404">The dimension manager offers no profile with this key.</response>
    /// <response code="409">The profile's dimension is defined already, or its key or a role key is taken.</response>
    [HttpPost("dimension-profiles/{profileKey}/define")]
    [ProducesResponseType<DimensionCreated>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status404NotFound, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<DimensionCreated>> Define(string tenantId, string profileKey, CancellationToken cancellationToken)
    {
        if (DimensionProfiles.Find(profileKey) is not { } profile || !DimensionProfiles.IsOffered(profile)) return Problems.OfStatus(StatusCodes.Status404NotFound).ToResult();
        return Created(await commands.DefineAsync(profile, cancellationToken));
    }

    /// <summary>Reads a dimension with its usage.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The detail.</returns>
    /// <response code="200">The detail read model.</response>
    [HttpGet("dimensions/{dimensionId:long}")]
    [ProducesResponseType<DimensionDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DimensionDetail>> Get(string tenantId, long dimensionId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.DetailAsync(dimensionId, cancellationToken));
    }

    /// <summary>Deletes a dimension nothing references, with its versions and roles.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Blocked (see <c>violations</c>), or owned by a report.</response>
    [HttpDelete("dimensions/{dimensionId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long dimensionId, CancellationToken cancellationToken)
    {
        return await commands.DeleteAsync(dimensionId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Checks whether a dimension can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check.</response>
    [HttpGet("dimensions/{dimensionId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckDelete(string tenantId, long dimensionId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.DimensionDeleteCheckAsync(dimensionId, cancellationToken));
    }

    /// <summary>Previews a dimension change set: applies the operations to the stored dimension in memory and writes nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The pending state and the findings that do not block saving.</returns>
    /// <response code="200">The operations apply.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">Owned by a report, locked by its profile, an operation targets a frozen version, or a delete is blocked.</response>
    /// <response code="422">The result would break a rule.</response>
    [HttpPost("dimensions/{dimensionId:long}/changes/preview")]
    [ProducesResponseType<ChangeSetPreview<DimensionEditState>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetPreview<DimensionEditState>>> Preview(
        string tenantId,
        long dimensionId,
        ChangeSetRequest<DimensionOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.PreviewAsync(dimensionId, request, cancellationToken));
    }

    /// <summary>Commits a dimension change set: applies the operations to the stored dimension and writes them as one batch.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored state and the real id of each temporary id.</returns>
    /// <response code="200">Every operation applied and was written.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">Owned by a report, locked by its profile, an operation targets a frozen version, or a delete is blocked.</response>
    /// <response code="422">The result would break a rule; nothing was written.</response>
    [HttpPost("dimensions/{dimensionId:long}/changes")]
    [ProducesResponseType<ChangeSetCommit<DimensionEditState>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetCommit<DimensionEditState>>> Commit(
        string tenantId,
        long dimensionId,
        ChangeSetRequest<DimensionOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.CommitAsync(dimensionId, request, cancellationToken));
    }

    /// <summary>Checks whether a role can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="roleId">The role.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check.</response>
    [HttpGet("dimension-roles/{roleId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckRoleDelete(string tenantId, long roleId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.RoleDeleteCheckAsync(roleId, cancellationToken));
    }

    /// <summary>201 with the created dimension, or the refusal's problem.</summary>
    private ActionResult<DimensionCreated> Created(Outcome<DimensionCreated> outcome)
    {
        return outcome.Succeeded ? StatusCode(StatusCodes.Status201Created, outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>200 with the value, or the refusal's problem.</summary>
    private ActionResult<T> OkOrProblem<T>(Outcome<T> outcome) where T : class
    {
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>200 with the value, or 404 for a report dimension's.</summary>
    private ActionResult<T> OkOrNotFound<T>(T? value) where T : class
    {
        return value is null ? Problems.OfStatus(StatusCodes.Status404NotFound).ToResult() : Ok(value);
    }
}
