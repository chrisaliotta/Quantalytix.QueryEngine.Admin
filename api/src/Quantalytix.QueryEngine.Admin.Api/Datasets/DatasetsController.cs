using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Api.Datasets;

/// <summary>Creates, edits and deletes datasets, the stable identities that query requests name, and lists and adds their versions.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/datasets")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class DatasetsController(DatasetService datasets, DatasetVersionService versions) : ControllerBase
{
    /// <summary>Lists every dataset of the tenant.</summary>
    /// <param name="tenantId">The tenant whose datasets are listed.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The datasets, sorted by key.</returns>
    /// <response code="200">The datasets, sorted by key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DatasetRecord>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<IReadOnlyList<DatasetRecord>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await datasets.ListAsync(cancellationToken));
    }

    /// <summary>Creates a dataset with an empty draft version 1 over a table or view of the tenant database.</summary>
    /// <param name="tenantId">The tenant that owns the dataset.</param>
    /// <param name="input">The dataset's identity and its first version's source object.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new dataset.</returns>
    /// <response code="200">The new dataset; its draft version 1 is listed under its versions.</response>
    /// <response code="400">The input breaks a field rule, the key is already used, or the source object does not exist.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<DatasetRecord>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DatasetRecord>> Create(string tenantId, DatasetInput input, CancellationToken cancellationToken)
    {
        return Ok(await datasets.CreateAsync(input, cancellationToken));
    }

    /// <summary>Reads one dataset.</summary>
    /// <param name="tenantId">The tenant that owns the dataset.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The dataset.</returns>
    /// <response code="200">The dataset.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{datasetId:long}")]
    [ProducesResponseType<DatasetRecord>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<DatasetRecord>> Get(string tenantId, long datasetId, CancellationToken cancellationToken)
    {
        return Ok(await datasets.GetAsync(datasetId, cancellationToken));
    }

    /// <summary>Changes a dataset's display name and availability, and advances the semantic revision.</summary>
    /// <param name="tenantId">The tenant that owns the dataset.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="input">The new identity attributes; the key is fixed.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated dataset.</returns>
    /// <response code="200">The updated dataset.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPut("{datasetId:long}")]
    [ProducesResponseType<DatasetRecord>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DatasetRecord>> Update(
        string tenantId,
        long datasetId,
        DatasetUpdateInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await datasets.UpdateAsync(datasetId, input, cancellationToken));
    }

    /// <summary>Deletes a dataset whose versions are all drafts and that no release lists, with all its versions' content.</summary>
    /// <param name="tenantId">The tenant that owns the dataset.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when a version is not a draft or a release lists the dataset.</returns>
    /// <response code="204">The dataset and its draft versions are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">A version is validated, published or retired, or a release lists the dataset; the detail names them.</response>
    [HttpDelete("{datasetId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long datasetId, CancellationToken cancellationToken)
    {
        var conflict = await datasets.DeleteAsync(datasetId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }

    /// <summary>Lists the versions of a dataset.</summary>
    /// <param name="tenantId">The tenant that owns the dataset.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The versions, by version number.</returns>
    /// <response code="200">The versions, by version number.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{datasetId:long}/versions")]
    [ProducesResponseType<IReadOnlyList<DatasetVersionRecord>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<IReadOnlyList<DatasetVersionRecord>>> ListVersions(
        string tenantId,
        long datasetId,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.ListAsync(datasetId, cancellationToken));
    }

    /// <summary>
    /// Creates a draft version with the dataset's next version number: a clone of an existing version's settings and content,
    /// or an empty version over a source object.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the dataset.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="input">The clone source, or the source object of an empty draft.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new draft version.</returns>
    /// <response code="200">The new draft version.</response>
    /// <response code="400">The clone source is not a version of this dataset, a clone names a source object, or an empty draft's source object does not exist.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost("{datasetId:long}/versions")]
    [ProducesResponseType<DatasetVersionRecord>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DatasetVersionRecord>> CreateVersion(
        string tenantId,
        long datasetId,
        DatasetVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.CreateAsync(datasetId, input, cancellationToken));
    }
}
