using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>
/// Lists, replaces and deletes the attributions of a calculation model's metrics: how each metric's change splits into the effects
/// of its drivers, and which variable weighs its member contributions. Each is keyed by its metric and written whole, drivers included.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/calculation-models/{modelId:long}/attributions")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class MetricAttributionsController(MetricAttributionService attributions, ModelChanges changes) : ControllerBase
{
    /// <summary>Lists a model's attributions, each with its drivers.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The attributions by metric key.</returns>
    /// <response code="200">The attributions by metric key, their drivers by sort order.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MetricAttribution>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MetricAttribution>>> List(string tenantId, long modelId, CancellationToken cancellationToken)
    {
        return Ok(await attributions.ListAsync(modelId, cancellationToken));
    }

    /// <summary>
    /// Sets a metric's attribution, with every one of its drivers, in place of the one stored, unless it breaks the model's rules:
    /// the metric, the expression's keys, the drivers' inputs and the weight variable must resolve in the model, and the drivers must
    /// split the expression's inputs among them.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="metricKey">The key of the variable or calculated measure attributed.</param>
    /// <param name="input">The attribution and its drivers.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored attribution.</returns>
    /// <response code="200">The stored attribution, its drivers by sort order.</response>
    /// <response code="400">The input breaks a field rule, such as an expression that does not parse or a driver keyed residual; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The attribution breaks a model rule, such as a key the model lacks, an input no driver switches, or POWER in a model of formula language 1.0; nothing was written.</response>
    [HttpPut("{metricKey}")]
    [AdvancesModelRevision("modelId")]
    [ProducesResponseType<MetricAttribution>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MetricAttribution>> Replace(
        string tenantId,
        long modelId,
        string metricKey,
        MetricAttributionInput input,
        CancellationToken cancellationToken)
    {
        var attribution = MetricAttributionService.Compose(metricKey, input);
        var violations = await changes.ApplyAsync(
            modelId,
            model => MetricAttributionService.Put(model, attribution),
            () => attributions.ReplaceAsync(modelId, attribution, cancellationToken),
            cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, modelId, violations);
        return Ok(attribution);
    }

    /// <summary>
    /// Deletes a metric's attribution with its drivers: a calculated measure then has the default drivers, one per reference of its
    /// formula, and no member contributions.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="metricKey">The key of the metric attributed.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 422 when nothing was deleted.</returns>
    /// <response code="204">The attribution is deleted, or the metric had none.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="422">The model breaks its rules without the attribution; nothing was deleted.</response>
    [HttpDelete("{metricKey}")]
    [AdvancesModelRevision("modelId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long modelId, string metricKey, CancellationToken cancellationToken)
    {
        var violations = await changes.ApplyAsync(
            modelId,
            model => MetricAttributionService.Without(model, metricKey),
            () => attributions.DeleteAsync(modelId, metricKey, cancellationToken),
            cancellationToken);
        if (violations.Count > 0) return ModelWriteResults.Rejected(HttpContext, modelId, violations);
        return NoContent();
    }
}
