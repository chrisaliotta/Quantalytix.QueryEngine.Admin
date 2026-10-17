using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>Lists, creates, edits and deletes calculation models: the variables and formulas reports evaluate.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/calculation-models")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class CalculationModelsController(CalculationModelService models) : ControllerBase
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

    /// <summary>Creates a calculation model; it has no versions until one is created.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="input">The new model's key, display name and availability.</param>
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

    /// <summary>Reads one calculation model, including its current version.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The model.</returns>
    /// <response code="200">The model.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{modelId:long}")]
    [ProducesResponseType<CalculationModelRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalculationModelRecord>> Get(string tenantId, long modelId, CancellationToken cancellationToken)
    {
        return Ok(await models.GetAsync(modelId, cancellationToken));
    }

    /// <summary>Edits a calculation model's display name and availability; reports pin model versions, so neither reaches queries.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored model.</returns>
    /// <response code="200">The stored model.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPut("{modelId:long}")]
    [ProducesResponseType<CalculationModelRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculationModelRecord>> Update(
        string tenantId,
        long modelId,
        CalculationModelInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await models.UpdateAsync(modelId, input, cancellationToken));
    }

    /// <summary>Deletes a calculation model whose versions are all drafts that no report reads, together with their content.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when something blocks the delete.</returns>
    /// <response code="204">The model and its drafts are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted; the detail names the non-draft versions and report versions that block the delete.</response>
    [HttpDelete("{modelId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long modelId, CancellationToken cancellationToken)
    {
        var conflict = await models.DeleteAsync(modelId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
