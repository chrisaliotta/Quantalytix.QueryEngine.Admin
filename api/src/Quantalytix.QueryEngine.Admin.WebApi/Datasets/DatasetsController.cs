using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Datasets;

/// <summary>The dataset manager (blueprint 11): the list, create, the dataset, its delete and delete check, and new drafts.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/datasets")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<Problem>(StatusCodes.Status401Unauthorized, ProblemWriter.ContentType)]
[ProducesResponseType<Problem>(StatusCodes.Status403Forbidden, ProblemWriter.ContentType)]
public sealed class DatasetsController(DatasetQueries queries, DatasetCommands commands) : ControllerBase
{
    /// <summary>Lists every dataset with its version summaries.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The list items by key.</returns>
    /// <response code="200">Every dataset of the tenant.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DatasetListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DatasetListItem>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ListAsync(cancellationToken));
    }

    /// <summary>Creates a dataset over an existing table or view, or over a new managed table, with draft version 1.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The new dataset.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The dataset.</returns>
    /// <response code="201">Created with draft version 1.</response>
    /// <response code="400">The input breaks a field rule.</response>
    /// <response code="409">The dataset key or the table name is taken.</response>
    [HttpPost]
    [ProducesResponseType<DatasetListItem>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<DatasetListItem>> Create(string tenantId, DatasetCreateInput input, CancellationToken cancellationToken)
    {
        return Created(await commands.CreateAsync(input, cancellationToken));
    }

    /// <summary>Reads one dataset with its version summaries.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The dataset.</returns>
    /// <response code="200">The dataset.</response>
    [HttpGet("{datasetId:long}")]
    [ProducesResponseType<DatasetListItem>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DatasetListItem>> Get(string tenantId, long datasetId, CancellationToken cancellationToken)
    {
        return Ok(await queries.GetAsync(datasetId, cancellationToken));
    }

    /// <summary>Deletes a dataset whose versions are all drafts or validated and that no release lists.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">Blocked; <c>violations</c> are the delete check's reasons.</response>
    [HttpDelete("{datasetId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long datasetId, CancellationToken cancellationToken)
    {
        return await commands.DeleteAsync(datasetId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Tells whether a dataset can be deleted, and why not.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check; codes <c>has_published_versions</c> and <c>referenced_by_release</c>.</response>
    [HttpGet("{datasetId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckDelete(string tenantId, long datasetId, CancellationToken cancellationToken)
    {
        return Ok(await queries.DatasetDeleteCheckAsync(datasetId, cancellationToken));
    }

    /// <summary>Creates a draft version, empty or cloned from another version of the dataset.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="input">The draft's source object, or the version to clone.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The draft.</returns>
    /// <response code="201">The new draft.</response>
    /// <response code="400">The input breaks a field rule, or the clone source is not a version of the dataset.</response>
    /// <response code="409">The dataset already has an open (draft or validated) version.</response>
    [HttpPost("{datasetId:long}/versions")]
    [ProducesResponseType<DatasetVersionSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<DatasetVersionSummary>> CreateDraft(string tenantId, long datasetId, DatasetCreateDraftInput input, CancellationToken cancellationToken)
    {
        return Created(await commands.CreateDraftAsync(datasetId, input, cancellationToken));
    }

    /// <summary>201 with the created value, or the refusal's problem.</summary>
    private ActionResult<T> Created<T>(Outcome<T> outcome) where T : class
    {
        return outcome.Succeeded ? StatusCode(StatusCodes.Status201Created, outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }
}
