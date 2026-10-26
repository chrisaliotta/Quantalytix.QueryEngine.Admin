using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>Lists and edits the variables of calculation models: the logical inputs each report fills from one of its own sources.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/calculation-models/{calculationModelId:long}/variables")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class VariablesController(IModelContent<VariableRecord, VariableInput> variables, ModelChanges changes) : ControllerBase
{
    /// <summary>Lists a model's variables.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The variables by key.</returns>
    /// <response code="200">The variables by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VariableRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VariableRecord>>> List(string tenantId, long calculationModelId, CancellationToken cancellationToken)
    {
        return Ok(await variables.ListAsync(calculationModelId, cancellationToken));
    }

    /// <summary>Adds a variable to a calculation model. A new variable breaks no formula and no report, so nothing checks it beyond its fields.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="input">The new variable.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created variable.</returns>
    /// <response code="200">The created variable.</response>
    /// <response code="400">The input breaks a field rule, such as a key another variable or calculated measure of the model has; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [AdvancesModelRevision("calculationModelId")]
    [ProducesResponseType<VariableRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<VariableRecord>> Create(string tenantId, long calculationModelId, VariableInput input, CancellationToken cancellationToken)
    {
        var created = variables.Compose(calculationModelId, null, input);
        await variables.InsertAsync(created, cancellationToken);
        return Ok(created);
    }

    /// <summary>Replaces a variable of a calculation model, keeping its ID, unless the change breaks the model's formulas or an enabled report.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="variable">The calculation model and its variable.</param>
    /// <param name="input">The variable's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored variable.</returns>
    /// <response code="200">The stored variable.</response>
    /// <response code="400">The variable is not the model's, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The change would break the model's formulas, such as a renamed key a formula reads, or an enabled report that reads the model; nothing was written.</response>
    [HttpPut("{itemId:long}")]
    [AdvancesModelRevision("calculationModelId")]
    [ProducesResponseType<VariableRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<VariableRecord>> Update(
        string tenantId,
        [FromRoute] ModelItemReference<VariableRecord> variable,
        VariableInput input,
        CancellationToken cancellationToken)
    {
        var updated = variables.Compose(variable.CalculationModelId, variable.ItemId, input);
        var violations = await changes.ApplyAsync(
            variable.CalculationModelId, model => variables.Put(model, updated), () => variables.UpdateAsync(updated, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, variable.CalculationModelId, violations);
        return Ok(updated);
    }

    /// <summary>Deletes a variable of a calculation model, unless a formula of the model or an enabled report reads it.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="variable">The calculation model and its variable.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 422 when nothing was deleted.</returns>
    /// <response code="204">The variable is deleted.</response>
    /// <response code="400">The variable is not the model's; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">A formula of the model or an enabled report that reads the model reads the variable; nothing was deleted.</response>
    [HttpDelete("{itemId:long}")]
    [AdvancesModelRevision("calculationModelId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] ModelItemReference<VariableRecord> variable, CancellationToken cancellationToken)
    {
        var violations = await changes.ApplyAsync(
            variable.CalculationModelId, model => variables.Without(model, variable.ItemId), () => variables.DeleteAsync(variable.ItemId, cancellationToken), cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, variable.CalculationModelId, violations);
        return NoContent();
    }
}
