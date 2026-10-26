using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;

namespace Quantalytix.QueryEngine.Admin.WebApi.CalculationModels;

/// <summary>
/// Lists, writes and deletes the parameters of a calculation model: the values its formulas read as <c>@key</c>, which each report
/// sets on its rows. Each is keyed by its key and written whole.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/calculation-models/{calculationModelId:long}/parameters")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ModelParametersController(ModelParameterService parameters, ModelChanges changes) : ControllerBase
{
    /// <summary>Lists a model's parameters.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The parameters by key.</returns>
    /// <response code="200">The parameters by key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ModelParameter>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ModelParameter>>> List(string tenantId, long calculationModelId, CancellationToken cancellationToken)
    {
        return Ok(await parameters.ListAsync(calculationModelId, cancellationToken));
    }

    /// <summary>
    /// Sets a parameter of a calculation model, creating it under the key or replacing the one stored, unless the change breaks the
    /// model's formulas or a report's row values: a formula must read the parameter where its new type belongs, and YEARFRAC a text
    /// parameter whose allowed values are day count conventions, and every value a report's rows set must fit its new type and allowed
    /// values.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="parameter">The calculation model and the parameter's key.</param>
    /// <param name="input">The parameter.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored parameter.</returns>
    /// <response code="200">The stored parameter.</response>
    /// <response code="400">The key or the input breaks a field rule, such as a number parameter with allowed values; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The change would break a formula of the model or a report's row values, such as a type a formula or row reads otherwise; nothing was written.</response>
    [HttpPut("{parameterKey}")]
    [AdvancesModelRevision("calculationModelId")]
    [ProducesResponseType<ModelParameter>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<ModelParameter>> Replace(
        string tenantId,
        [FromRoute] ModelParameterReference parameter,
        ModelParameterInput input,
        CancellationToken cancellationToken)
    {
        var stored = ModelParameterService.Compose(parameter.ParameterKey, input);
        var violations = await changes.ApplyAsync(
            parameter.CalculationModelId,
            model => ModelParameterService.Put(model, stored),
            () => parameters.ReplaceAsync(parameter.CalculationModelId, stored, cancellationToken),
            cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, parameter.CalculationModelId, violations);
        return Ok(stored);
    }

    /// <summary>Deletes a parameter of a calculation model, unless a formula of the model reads it or a report's rows set it.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="parameterKey">The parameter's key.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 422 when nothing was deleted.</returns>
    /// <response code="204">The parameter is deleted, or the model had none by the key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">A formula of the model reads the parameter or a report's rows set it; nothing was deleted.</response>
    [HttpDelete("{parameterKey}")]
    [AdvancesModelRevision("calculationModelId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long calculationModelId, string parameterKey, CancellationToken cancellationToken)
    {
        var violations = await changes.ApplyAsync(
            calculationModelId,
            model => ModelParameterService.Without(model, parameterKey),
            () => parameters.DeleteAsync(calculationModelId, parameterKey, cancellationToken),
            cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, calculationModelId, violations);
        return NoContent();
    }
}
