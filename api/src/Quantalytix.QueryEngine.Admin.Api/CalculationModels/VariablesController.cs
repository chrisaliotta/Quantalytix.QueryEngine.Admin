using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>Lists and edits the variables of calculation model versions: the logical inputs a profile fills from one of their sources.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/model-versions/{versionId:long}/variables")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class VariablesController(IModelVersionContent<VariableRecord, VariableInput> variables) : ControllerBase
{
    /// <summary>Lists a model version's variables.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The variables by key.</returns>
    /// <response code="200">The variables by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VariableRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VariableRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await variables.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Adds a variable to a draft model version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="input">The new variable.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created variable.</returns>
    /// <response code="200">The created variable.</response>
    /// <response code="400">The input breaks a field rule, such as a key another variable or calculated measure of the version has; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPost]
    [RequireDraft(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType<VariableRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<VariableRecord>> Create(string tenantId, long versionId, VariableInput input, CancellationToken cancellationToken)
    {
        return Ok(await variables.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>Replaces a variable of a draft model version, keeping its ID.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="variable">The draft model version and its variable.</param>
    /// <param name="input">The variable's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored variable.</returns>
    /// <response code="200">The stored variable.</response>
    /// <response code="400">The variable is not in the version, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("{itemId:long}")]
    [RequireDraft(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType<VariableRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<VariableRecord>> Update(
        string tenantId,
        [FromRoute] ModelItemReference<VariableRecord> variable,
        VariableInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await variables.UpdateAsync(variable.VersionId, variable.ItemId, input, cancellationToken));
    }

    /// <summary>Deletes a variable of a draft model version with its sources and their profile picks and override routes, unless a report column returns it.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="variable">The draft model version and its variable.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when nothing was deleted.</returns>
    /// <response code="204">The variable is deleted.</response>
    /// <response code="400">The variable is not in the version; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft, or the detail names the report columns that return the variable.</response>
    [HttpDelete("{itemId:long}")]
    [RequireDraft(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] ModelItemReference<VariableRecord> variable, CancellationToken cancellationToken)
    {
        var conflict = await variables.DeleteAsync(variable.ItemId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
