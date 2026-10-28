using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Datasets;

/// <summary>
/// Dataset versions (blueprint 11): the content, its change set, the rule check, the strict lifecycle (validate, publish, reopen,
/// retire), deleting an open version, and the materialization of JSON path fields (OI-32).
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/dataset-versions/{versionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<Problem>(StatusCodes.Status401Unauthorized, ProblemWriter.ContentType)]
[ProducesResponseType<Problem>(StatusCodes.Status403Forbidden, ProblemWriter.ContentType)]
public sealed class DatasetVersionsController(
    DatasetQueries queries,
    DatasetCommands commands,
    DatasetChanges changes,
    DatasetMaterializations materializations) : ControllerBase
{
    /// <summary>Reads a version's settings, fields, grains, bindings, measures and dependents.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The content.</returns>
    /// <response code="200">The content.</response>
    [HttpGet]
    [ProducesResponseType<DatasetVersion>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DatasetVersion>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.VersionAsync(versionId, cancellationToken));
    }

    /// <summary>Deletes a draft or validated version that no release pins.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Blocked; <c>violations</c> are the delete check's reasons.</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return await commands.DeleteVersionAsync(versionId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Tells whether a version can be deleted, and why not.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check; codes <c>published_version</c> and <c>referenced_by_release</c>.</response>
    [HttpGet("delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckDelete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.VersionDeleteCheckAsync(versionId, cancellationToken));
    }

    /// <summary>Previews a change set: applies the operations to the stored version in memory and writes nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The pending content and the rule findings that do not block saving.</returns>
    /// <response code="200">The operations apply.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">Not a draft (only <c>updateDataset</c> applies), not a managed table, or a delete is blocked.</response>
    /// <response code="422">The result would break a rule.</response>
    [HttpPost("changes/preview")]
    [ProducesResponseType<ChangeSetPreview<DatasetVersion>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetPreview<DatasetVersion>>> Preview(
        string tenantId,
        long versionId,
        ChangeSetRequest<DatasetOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.PreviewAsync(versionId, request, cancellationToken));
    }

    /// <summary>Commits a change set: runs its DDL, then applies the operations to the stored version and writes them.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored content and the real id of each temporary id.</returns>
    /// <response code="200">Every operation applied and was written.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">Not a draft (only <c>updateDataset</c> applies), not a managed table, or a delete is blocked.</response>
    /// <response code="422">The result would break a rule; nothing was written.</response>
    [HttpPost("changes")]
    [ProducesResponseType<ChangeSetCommit<DatasetVersion>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetCommit<DatasetVersion>>> Commit(
        string tenantId,
        long versionId,
        ChangeSetRequest<DatasetOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.CommitAsync(versionId, request, cancellationToken));
    }

    /// <summary>Runs every dataset rule on the version and changes nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The rule report.</returns>
    /// <response code="200">The rule report; each violation's <c>target.area</c> is a dataset tab.</response>
    /// <response code="400">The route does not bind.</response>
    [HttpPost("check")]
    [ProducesResponseType<ValidationReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<ValidationReport>> Check(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await queries.CheckAsync(versionId, cancellationToken));
    }

    /// <summary>Moves a draft to validated when every rule passes.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The draft.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The validated version.</returns>
    /// <response code="200">Validated.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not a draft.</response>
    /// <response code="422">A rule fails; nothing was written.</response>
    [HttpPost("validate")]
    [ProducesResponseType<DatasetVersionSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<DatasetVersionSummary>> Validate(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.ValidateAsync(versionId, cancellationToken));
    }

    /// <summary>Publishes a validated version; a draft is validated first, in the same call.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The draft or validated version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The published version.</returns>
    /// <response code="200">Published.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not a draft or validated.</response>
    /// <response code="422">A rule fails; nothing was written.</response>
    [HttpPost("publish")]
    [ProducesResponseType<DatasetVersionSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<DatasetVersionSummary>> Publish(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.PublishAsync(versionId, cancellationToken));
    }

    /// <summary>Returns a validated version to draft.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The validated version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The draft.</returns>
    /// <response code="200">Back to draft.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not validated.</response>
    [HttpPost("reopen")]
    [ProducesResponseType<DatasetVersionSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<DatasetVersionSummary>> Reopen(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.ReopenAsync(versionId, cancellationToken));
    }

    /// <summary>Retires a published version that the active release does not pin.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The published version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The retired version.</returns>
    /// <response code="200">Retired.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Not published.</response>
    /// <response code="422">The active release pins the version.</response>
    [HttpPost("retire")]
    [ProducesResponseType<DatasetVersionSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<DatasetVersionSummary>> Retire(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.RetireAsync(versionId, cancellationToken));
    }

    /// <summary>Adds a JSON path field's column to its source object's materialized view; StarRocks builds it asynchronously.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The field's version.</param>
    /// <param name="fieldId">The field.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The field with its materialization.</returns>
    /// <response code="200">The field; poll the version for <c>ready</c> or <c>failed</c>.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The version is retired or the field is not a JSON path field.</response>
    [HttpPost("fields/{fieldId:long}/materialization")]
    [ProducesResponseType<DatasetField>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<DatasetField>> Materialize(string tenantId, long versionId, long fieldId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await materializations.MaterializeAsync(versionId, fieldId, cancellationToken));
    }

    /// <summary>Removes a field's column from the materialized view; the field reads its JSON path again.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The field's version.</param>
    /// <param name="fieldId">The field.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The field without materialization.</returns>
    /// <response code="200">The field.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The version is retired or the field is not a JSON path field.</response>
    [HttpDelete("fields/{fieldId:long}/materialization")]
    [ProducesResponseType<DatasetField>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<DatasetField>> Dematerialize(string tenantId, long versionId, long fieldId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await materializations.DematerializeAsync(versionId, fieldId, cancellationToken));
    }

    /// <summary>Starts a refresh of a materialized field's view.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The field's version.</param>
    /// <param name="fieldId">The field.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The field.</returns>
    /// <response code="200">The field, building.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The version is retired or the field is not materialized.</response>
    [HttpPost("fields/{fieldId:long}/materialization/refresh")]
    [ProducesResponseType<DatasetField>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<DatasetField>> Refresh(string tenantId, long versionId, long fieldId, CancellationToken cancellationToken)
    {
        return OkOrProblem(await materializations.RefreshAsync(versionId, fieldId, cancellationToken));
    }

    /// <summary>200 with the value, or the refusal's problem.</summary>
    private ActionResult<T> OkOrProblem<T>(Outcome<T> outcome) where T : class
    {
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }
}
