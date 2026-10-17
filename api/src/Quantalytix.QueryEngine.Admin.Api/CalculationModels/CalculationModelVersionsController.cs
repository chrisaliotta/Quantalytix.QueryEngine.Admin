using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>
/// Lists, creates, clones, edits and deletes calculation model versions. Status changes go through the versions status endpoint.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class CalculationModelVersionsController(CalculationModelVersionService versions) : ControllerBase
{
    /// <summary>Lists a calculation model's versions with their currency and status.</summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The versions by version number.</returns>
    /// <response code="200">The versions by version number.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("calculation-models/{modelId:long}/versions")]
    [ProducesResponseType<IReadOnlyList<CalculationModelVersionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalculationModelVersionRecord>>> List(string tenantId, long modelId, CancellationToken cancellationToken)
    {
        return Ok(await versions.ListAsync(modelId, cancellationToken));
    }

    /// <summary>
    /// Creates a draft with the model's next version number: empty, or cloned from <c>cloneFromVersionId</c> with all its
    /// variables, sources, profiles, calculated measures, calculation dimensions and routes.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the model.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="input">The currency and optional clone source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created draft.</returns>
    /// <response code="200">The created draft.</response>
    /// <response code="400">The input breaks a field rule, such as a clone source of another model; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost("calculation-models/{modelId:long}/versions")]
    [ProducesResponseType<CalculationModelVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculationModelVersionRecord>> Create(
        string tenantId,
        long modelId,
        NewModelVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.CreateAsync(modelId, input, cancellationToken));
    }

    /// <summary>Reads one calculation model version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version.</returns>
    /// <response code="200">The version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("model-versions/{versionId:long}")]
    [ProducesResponseType<CalculationModelVersionRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalculationModelVersionRecord>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await versions.GetAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces a draft version's currency; its content stays as it is.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored version.</returns>
    /// <response code="200">The stored version.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("model-versions/{versionId:long}")]
    [RequireDraft(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType<CalculationModelVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculationModelVersionRecord>> Update(
        string tenantId,
        long versionId,
        ModelVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.UpdateAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Deletes a draft version that no report version reads, with its content, and clears the model's current-version pointer
    /// when it named the version.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the version is not a draft or a report version reads it.</returns>
    /// <response code="204">The version and its content are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft, or the detail names the report versions that read it.</response>
    [HttpDelete("model-versions/{versionId:long}")]
    [RequireDraft(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        var conflict = await versions.DeleteAsync(versionId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
