using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.WebApi.CalculationModels;

/// <summary>Lists, creates, edits and deletes calculation models: the variables and formulas reports evaluate, edited in place.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/calculation-models")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class CalculationModelsController(CalculationModelService models, ModelChanges changes) : ControllerBase
{
    /// <summary>Lists every calculation model of the tenant.</summary>
    /// <param name="tenantId">The tenant that owns the models.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The models by key.</returns>
    /// <response code="200">The models by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CalculationModelRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalculationModelRecord>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await models.ListAsync(cancellationToken));
    }

    /// <summary>Creates a calculation model without variables or calculated measures.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="input">The new model's key, display name, currency, availability, formula language and attribution method.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created model.</returns>
    /// <response code="200">The created model.</response>
    /// <response code="400">The input breaks a field rule, such as a taken key; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<CalculationModelRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculationModelRecord>> Create(string tenantId, NewCalculationModelInput input, CancellationToken cancellationToken)
    {
        return Ok(await models.CreateAsync(input, cancellationToken));
    }

    /// <summary>Reads one calculation model.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The model.</returns>
    /// <response code="200">The model.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{calculationModelId:long}")]
    [ProducesResponseType<CalculationModelRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalculationModelRecord>> Get(string tenantId, long calculationModelId, CancellationToken cancellationToken)
    {
        return Ok(await models.GetAsync(calculationModelId, cancellationToken));
    }

    /// <summary>
    /// Edits a calculation model's display name, currency, availability, formula language and attribution method; the enabled reports
    /// that read it show its new currency and attribute by its new method at once. A formula language that lacks a function the
    /// model's formulas call is refused.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored model.</returns>
    /// <response code="200">The stored model.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The model's formulas or attribution expressions call POWER, which formula language 1.0 lacks; nothing was written.</response>
    [HttpPut("{calculationModelId:long}")]
    [AdvancesModelRevision("calculationModelId")]
    [ProducesResponseType<CalculationModelRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CalculationModelRecord>> Update(
        string tenantId,
        long calculationModelId,
        CalculationModelInput input,
        CancellationToken cancellationToken)
    {
        var updated = await models.ComposeAsync(calculationModelId, input, cancellationToken);
        var violations = await changes.ApplyAsync(
            calculationModelId, model => model with { Model = updated }, () => models.UpdateAsync(updated, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, calculationModelId, violations);
        return Ok(updated);
    }

    /// <summary>Deletes a calculation model that no report reads, together with its variables, calculated measures and attributions.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when something blocks the delete.</returns>
    /// <response code="204">The model, its metrics and their attributions are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted; the detail names the reports that read the model.</response>
    [HttpDelete("{calculationModelId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long calculationModelId, CancellationToken cancellationToken)
    {
        var conflict = await models.DeleteAsync(calculationModelId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
