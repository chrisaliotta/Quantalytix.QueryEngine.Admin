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
/// Dataset versions (blueprint 11): the content of a live or frozen version, the live version's change set, and the materialization
/// of its JSON path fields (OI-32). A frozen version refuses every write with 409.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/dataset-versions/{versionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class DatasetVersionsController(
    DatasetQueries queries,
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

    /// <summary>Previews a change set: applies the operations to the stored version in memory and writes nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The pending content and the rule findings that do not block saving.</returns>
    /// <response code="200">The operations apply.</response>
    /// <response code="400">An operation's input is invalid; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="409">The version is frozen, the table is not a managed one, or a delete is blocked.</response>
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
    /// <response code="409">The version is frozen, the table is not a managed one, or a delete is blocked.</response>
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

    /// <summary>Adds a JSON path field's column to its source object's materialized view; StarRocks builds it asynchronously.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The field's version.</param>
    /// <param name="fieldId">The field.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The field with its materialization.</returns>
    /// <response code="200">The field; poll the version for <c>ready</c> or <c>failed</c>.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The version is frozen or the field is not a JSON path field.</response>
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
    /// <response code="409">The version is frozen or the field is not a JSON path field.</response>
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
    /// <response code="409">The version is frozen or the field is not materialized.</response>
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
