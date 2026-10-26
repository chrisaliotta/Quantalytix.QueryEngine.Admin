using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.WebApi.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.WebApi.Datasets;

/// <summary>Edits the grains of dataset versions: the ordered field sets that identify rows and temporal series.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/dataset-versions/{versionId:long}/grains")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class GrainsController(GrainService grains) : ControllerBase
{
    /// <summary>Lists the grains of a dataset version, each with its fields in ordinal order.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The grains, sorted by key.</returns>
    /// <response code="200">The grains, sorted by key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GrainDetail>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GrainDetail>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await grains.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Creates a grain in a draft dataset version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft dataset version.</param>
    /// <param name="input">The new grain and its fields in order.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new grain.</returns>
    /// <response code="200">The new grain.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPost]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<GrainDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<GrainDetail>> Create(string tenantId, long versionId, GrainInput input, CancellationToken cancellationToken)
    {
        return Ok(await grains.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>Changes a grain's kind and replaces its fields; the key is fixed.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its grain.</param>
    /// <param name="input">The new kind and fields in order.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated grain.</returns>
    /// <response code="200">The updated grain.</response>
    /// <response code="400">The grain is not in the version, or the input breaks a field rule.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("{datasetGrainId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<GrainDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<GrainDetail>> Update(
        string tenantId,
        [FromRoute] GrainReference reference,
        GrainUpdateInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await grains.UpdateAsync(reference, input, cancellationToken));
    }

    /// <summary>Deletes a grain that no measure time policy selects observations by.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its grain.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when a measure time policy uses the grain.</returns>
    /// <response code="204">The grain and its fields are deleted.</response>
    /// <response code="400">The grain is not in the version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft, or a measure time policy's selection grain is this grain; the detail names the measures.</response>
    [HttpDelete("{datasetGrainId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] GrainReference reference, CancellationToken cancellationToken)
    {
        var conflict = await grains.DeleteAsync(reference, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
