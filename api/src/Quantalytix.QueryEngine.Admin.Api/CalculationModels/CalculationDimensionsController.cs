using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>Lists and edits the calculation dimensions of calculation model versions: the canonical dimensions a report can break out or filter by.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/model-versions/{versionId:long}/calculation-dimensions")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class CalculationDimensionsController(IModelVersionContent<CalculationDimensionRecord, CalculationDimensionInput> dimensions) : ControllerBase
{
    /// <summary>Lists a model version's calculation dimensions.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The calculation dimensions by key.</returns>
    /// <response code="200">The calculation dimensions by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CalculationDimensionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalculationDimensionRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await dimensions.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Adds a calculation dimension to a draft model version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="input">The new calculation dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created calculation dimension.</returns>
    /// <response code="200">The created calculation dimension.</response>
    /// <response code="400">The input breaks a field rule, such as a taken key; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPost]
    [RequireDraft(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType<CalculationDimensionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculationDimensionRecord>> Create(string tenantId, long versionId, CalculationDimensionInput input, CancellationToken cancellationToken)
    {
        return Ok(await dimensions.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>Replaces a calculation dimension of a draft model version, keeping its ID.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimension">The draft model version and its calculation dimension.</param>
    /// <param name="input">The calculation dimension's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored calculation dimension.</returns>
    /// <response code="200">The stored calculation dimension.</response>
    /// <response code="400">The calculation dimension is not in the version, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("{itemId:long}")]
    [RequireDraft(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType<CalculationDimensionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculationDimensionRecord>> Update(
        string tenantId,
        [FromRoute] ModelItemReference<CalculationDimensionRecord> dimension,
        CalculationDimensionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await dimensions.UpdateAsync(dimension.VersionId, dimension.ItemId, input, cancellationToken));
    }

    /// <summary>Deletes a calculation dimension of a draft model version with its routes.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimension">The draft model version and its calculation dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when nothing was deleted.</returns>
    /// <response code="204">The calculation dimension is deleted.</response>
    /// <response code="400">The calculation dimension is not in the version; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft.</response>
    [HttpDelete("{itemId:long}")]
    [RequireDraft(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] ModelItemReference<CalculationDimensionRecord> dimension, CancellationToken cancellationToken)
    {
        var conflict = await dimensions.DeleteAsync(dimension.ItemId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
