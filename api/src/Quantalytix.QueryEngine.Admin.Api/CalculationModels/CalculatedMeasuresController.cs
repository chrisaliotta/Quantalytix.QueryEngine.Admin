using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>Lists and edits the calculated measures of calculation model versions: formulas over variables and other calculated measures.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/model-versions/{versionId:long}/calculated-measures")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class CalculatedMeasuresController(IOwnedContent<CalculatedMeasureRecord, CalculatedMeasureInput> calculatedMeasures) : ControllerBase
{
    /// <summary>Lists a model version's calculated measures.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The calculated measures by key.</returns>
    /// <response code="200">The calculated measures by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CalculatedMeasureRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalculatedMeasureRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await calculatedMeasures.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Adds a calculated measure to a draft or published model version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published model version.</param>
    /// <param name="input">The new calculated measure.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created calculated measure.</returns>
    /// <response code="200">The created calculated measure.</response>
    /// <response code="400">The input breaks a field rule, such as a formula that does not parse or reads a dataset field; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPost]
    [RequireEditable(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType<CalculatedMeasureRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculatedMeasureRecord>> Create(string tenantId, long versionId, CalculatedMeasureInput input, CancellationToken cancellationToken)
    {
        var created = calculatedMeasures.Compose(versionId, null, input);
        await calculatedMeasures.InsertAsync(created, cancellationToken);
        return Ok(created);
    }

    /// <summary>Replaces a calculated measure of a draft or published model version, keeping its ID.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="measure">The draft or published model version and its calculated measure.</param>
    /// <param name="input">The calculated measure's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored calculated measure.</returns>
    /// <response code="200">The stored calculated measure.</response>
    /// <response code="400">The calculated measure is not in the version, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("{itemId:long}")]
    [RequireEditable(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType<CalculatedMeasureRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculatedMeasureRecord>> Update(
        string tenantId,
        [FromRoute] VersionItemReference<CalculatedMeasureRecord> measure,
        CalculatedMeasureInput input,
        CancellationToken cancellationToken)
    {
        var updated = calculatedMeasures.Compose(measure.VersionId, measure.ItemId, input);
        await calculatedMeasures.UpdateAsync(updated, cancellationToken);
        return Ok(updated);
    }

    /// <summary>Deletes a calculated measure of a draft or published model version, unless a live report column returns it or a live formula row evaluates it.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="measure">The draft or published model version and its calculated measure.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when nothing was deleted.</returns>
    /// <response code="204">The calculated measure is deleted.</response>
    /// <response code="400">The calculated measure is not in the version; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is neither a draft nor published, or the detail names the report columns and lines that use the measure.</response>
    [HttpDelete("{itemId:long}")]
    [RequireEditable(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] VersionItemReference<CalculatedMeasureRecord> measure, CancellationToken cancellationToken)
    {
        var conflict = await calculatedMeasures.DeleteAsync(measure.ItemId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
