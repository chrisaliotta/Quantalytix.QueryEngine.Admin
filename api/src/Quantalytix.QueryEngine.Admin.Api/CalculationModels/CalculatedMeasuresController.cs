using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>Lists and edits the calculated measures of calculation models: formulas over variables and other calculated measures.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/calculation-models/{modelId:long}/calculated-measures")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class CalculatedMeasuresController(IModelContent<CalculatedMeasureRecord, CalculatedMeasureInput> calculatedMeasures, ModelChanges changes)
    : ControllerBase
{
    /// <summary>Lists a model's calculated measures.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The calculated measures by key.</returns>
    /// <response code="200">The calculated measures by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CalculatedMeasureRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalculatedMeasureRecord>>> List(string tenantId, long modelId, CancellationToken cancellationToken)
    {
        return Ok(await calculatedMeasures.ListAsync(modelId, cancellationToken));
    }

    /// <summary>Adds a calculated measure to a calculation model, unless its formula reads a key the model lacks or reads itself.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="input">The new calculated measure.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created calculated measure.</returns>
    /// <response code="200">The created calculated measure.</response>
    /// <response code="400">The input breaks a field rule, such as a formula that does not parse or reads a dataset field; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The formula reads a variable or calculated measure the model lacks, or reads itself; nothing was written.</response>
    [HttpPost]
    [AdvancesModelRevision("modelId")]
    [ProducesResponseType<CalculatedMeasureRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CalculatedMeasureRecord>> Create(string tenantId, long modelId, CalculatedMeasureInput input, CancellationToken cancellationToken)
    {
        var created = calculatedMeasures.Compose(modelId, null, input);
        var violations = await changes.ApplyAsync(
            modelId, model => calculatedMeasures.Put(model, created), () => calculatedMeasures.InsertAsync(created, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, modelId, violations);
        return Ok(created);
    }

    /// <summary>Replaces a calculated measure of a calculation model, keeping its ID, unless the change breaks the model's formulas or an enabled report.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="measure">The calculation model and its calculated measure.</param>
    /// <param name="input">The calculated measure's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored calculated measure.</returns>
    /// <response code="200">The stored calculated measure.</response>
    /// <response code="400">The calculated measure is not the model's, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The change would break the model's formulas or an enabled report that reads the model, such as a formula that now reads a variable no profile picks a source for; nothing was written.</response>
    [HttpPut("{itemId:long}")]
    [AdvancesModelRevision("modelId")]
    [ProducesResponseType<CalculatedMeasureRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CalculatedMeasureRecord>> Update(
        string tenantId,
        [FromRoute] ModelItemReference<CalculatedMeasureRecord> measure,
        CalculatedMeasureInput input,
        CancellationToken cancellationToken)
    {
        var updated = calculatedMeasures.Compose(measure.ModelId, measure.ItemId, input);
        var violations = await changes.ApplyAsync(
            measure.ModelId, model => calculatedMeasures.Put(model, updated), () => calculatedMeasures.UpdateAsync(updated, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, measure.ModelId, violations);
        return Ok(updated);
    }

    /// <summary>Deletes a calculated measure of a calculation model, unless another formula of the model or an enabled report reads it.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="measure">The calculation model and its calculated measure.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 422 when nothing was deleted.</returns>
    /// <response code="204">The calculated measure is deleted.</response>
    /// <response code="400">The calculated measure is not the model's; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">Another formula of the model, or a column or formula row of an enabled report that reads the model, reads the measure; nothing was deleted.</response>
    [HttpDelete("{itemId:long}")]
    [AdvancesModelRevision("modelId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] ModelItemReference<CalculatedMeasureRecord> measure, CancellationToken cancellationToken)
    {
        var violations = await changes.ApplyAsync(
            measure.ModelId,
            model => calculatedMeasures.Without(model, measure.ItemId),
            () => calculatedMeasures.DeleteAsync(measure.ItemId, cancellationToken),
            cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, measure.ModelId, violations);
        return NoContent();
    }
}
