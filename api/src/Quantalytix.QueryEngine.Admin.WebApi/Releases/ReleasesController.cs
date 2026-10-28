using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Releases;

/// <summary>
/// Semantic releases (blueprint 14): the list, a release, the diff of two releases, the manifest change set, the rule check, the
/// strict lifecycle (validate, publish, reopen, retire), activation and deleting a draft.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/releases")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class ReleasesController(ReleaseQueries queries, ReleaseCommands commands, ReleaseChanges changes, CallerFactory callers) : ControllerBase
{
    /// <summary>Lists every release, newest first, with its activity and its difference from the active release.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The releases.</returns>
    /// <response code="200">Every release, newest first.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReleaseListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReleaseListItem>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ListAsync(cancellationToken));
    }

    /// <summary>Creates a draft release, empty or copying another release's manifest.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The key and the release to copy.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new draft.</returns>
    /// <response code="201">The new draft.</response>
    /// <response code="400">The key is malformed or the release to copy does not exist.</response>
    /// <response code="409">The key is taken.</response>
    [HttpPost]
    [ProducesResponseType<Release>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<Release>> Create(string tenantId, CreateReleaseInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.CreateAsync(input, cancellationToken);
        if (!outcome.Succeeded) return Problems.From(outcome.Refusal).ToResult();
        return CreatedAtAction(nameof(Get), new { tenantId, releaseId = outcome.Value.Id }, outcome.Value);
    }

    /// <summary>Compares the manifests of two releases.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="from">The release compared from.</param>
    /// <param name="to">The release compared to.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The difference.</returns>
    /// <response code="200">What changes going from <c>from</c> to <c>to</c>; removals name the enabled reports that read them.</response>
    /// <response code="404"><c>from</c> or <c>to</c> names no release.</response>
    [HttpGet("diff")]
    [ProducesResponseType<ReleaseDiff>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status404NotFound, ProblemWriter.ContentType)]
    public async Task<ActionResult<ReleaseDiff>> Diff(
        string tenantId,
        [FromQuery, BindRequired] long from,
        [FromQuery, BindRequired] long to,
        CancellationToken cancellationToken)
    {
        return await queries.DiffAsync(from, to, cancellationToken) is { } diff ? Ok(diff) : Problems.OfStatus(StatusCodes.Status404NotFound).ToResult();
    }

    /// <summary>Reads a release with its manifest and activation history.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The release.</returns>
    /// <response code="200">The release.</response>
    [HttpGet("{releaseId:long}")]
    [ProducesResponseType<ReleaseDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReleaseDetail>> Get(string tenantId, long releaseId, CancellationToken cancellationToken)
    {
        return Ok(await queries.GetAsync(releaseId, cancellationToken));
    }

    /// <summary>Deletes a draft release and its manifest.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The draft.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not a draft.</response>
    [HttpDelete("{releaseId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long releaseId, CancellationToken cancellationToken)
    {
        return await commands.DeleteAsync(releaseId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Applies staged manifest operations in memory, then checks and diffs the result; writes nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The draft.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The pending release, its report and its difference from the active release.</returns>
    /// <response code="200">The operations apply.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">Not a draft, or a pinned dataset no longer exists.</response>
    /// <response code="422">A pinned version is not one of its dataset's versions or is not published.</response>
    [HttpPost("{releaseId:long}/changes/preview")]
    [ProducesResponseType<ReleaseChangeSetPreview>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ReleaseChangeSetPreview>> Preview(
        string tenantId,
        long releaseId,
        ChangeSetRequest<ReleaseOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.PreviewAsync(releaseId, request, cancellationToken));
    }

    /// <summary>Applies staged manifest operations to the draft as stored now and writes them.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The draft.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored release.</returns>
    /// <response code="200">Every operation applied and was written.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">Not a draft, or a pinned dataset no longer exists.</response>
    /// <response code="422">A pinned version is not one of its dataset's versions or is not published; nothing was written.</response>
    [HttpPost("{releaseId:long}/changes")]
    [ProducesResponseType<ChangeSetCommit<ReleaseDetail>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetCommit<ReleaseDetail>>> Commit(
        string tenantId,
        long releaseId,
        ChangeSetRequest<ReleaseOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.CommitAsync(releaseId, request, cancellationToken));
    }

    /// <summary>Runs every release rule over the saved manifest and changes nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The report.</returns>
    /// <response code="200">The findings; <c>unsaved</c> is false.</response>
    /// <response code="400">The route does not bind.</response>
    [HttpPost("{releaseId:long}/check")]
    [ProducesResponseType<ReleaseCheckReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<ReleaseCheckReport>> Check(string tenantId, long releaseId, CancellationToken cancellationToken)
    {
        return Ok(await queries.CheckAsync(releaseId, cancellationToken));
    }

    /// <summary>Moves a draft to validated when every release rule passes.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The draft.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The validated release and its report.</returns>
    /// <response code="200">Validated; the report has no violations.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not a draft.</response>
    /// <response code="422">A release rule fails; nothing was written.</response>
    [HttpPost("{releaseId:long}/validate")]
    [ProducesResponseType<ReleaseTransitionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ReleaseTransitionResult>> Validate(string tenantId, long releaseId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.ValidateAsync(releaseId, cancellationToken));
    }

    /// <summary>Publishes a validated release and computes its content hash; activates and retires nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The validated release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The published release and its report.</returns>
    /// <response code="200">Published; <c>release.contentHash</c> is set.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not validated.</response>
    /// <response code="422">A release rule fails; nothing was written.</response>
    [HttpPost("{releaseId:long}/publish")]
    [ProducesResponseType<ReleaseTransitionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ReleaseTransitionResult>> Publish(string tenantId, long releaseId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.PublishAsync(releaseId, cancellationToken));
    }

    /// <summary>Returns a validated release to draft.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The validated release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The draft.</returns>
    /// <response code="200">A draft again.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not validated.</response>
    [HttpPost("{releaseId:long}/reopen")]
    [ProducesResponseType<Release>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<Release>> Reopen(string tenantId, long releaseId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.ReopenAsync(releaseId, cancellationToken));
    }

    /// <summary>Retires a published release that is not the active one.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The published release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The retired release.</returns>
    /// <response code="200">Retired.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not published.</response>
    /// <response code="422"><c>release_not_active</c>: the release is the active one.</response>
    [HttpPost("{releaseId:long}/retire")]
    [ProducesResponseType<Release>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<Release>> Retire(string tenantId, long releaseId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.RetireAsync(releaseId, cancellationToken));
    }

    /// <summary>
    /// Makes a published release the active one: re-checks every enabled report against it, sets the tenant's active release, advances
    /// the semantic revision and logs the activation. Activating an older release is a rollback.
    /// </summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="releaseId">The published release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The tenant state the activation produced.</returns>
    /// <response code="200">The new <c>activeReleaseId</c> and the advanced <c>semanticRevision</c>.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not published.</response>
    /// <response code="422">An enabled report's rules fail on the release; nothing was written.</response>
    [HttpPost("{releaseId:long}/activate")]
    [ProducesResponseType<TenantInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<TenantInfo>> Activate(string tenantId, long releaseId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.ActivateAsync(releaseId, callers.Create(User).Subject, cancellationToken));
    }

    /// <summary>200 with the value, or the refusal's problem.</summary>
    private ActionResult<T> OkOrProblem<T>(Outcome<T> outcome) where T : class
    {
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }
}
