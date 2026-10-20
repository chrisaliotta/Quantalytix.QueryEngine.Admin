using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>Lists and edits the variable sources of calculation model versions: the dataset, report field, time field, measure and filter that can fill a variable.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/model-versions/{versionId:long}/variable-sources")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class VariableSourcesController(IModelVersionContent<VariableSourceRecord, VariableSourceInput> sources) : ControllerBase
{
    /// <summary>Lists a model version's variable sources.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The variable sources by key.</returns>
    /// <response code="200">The sources by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VariableSourceRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VariableSourceRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await sources.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Adds a variable source to a draft or published model version; whether its keys resolve in the dataset version a release pins is a release rule.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published model version.</param>
    /// <param name="input">The new source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created source.</returns>
    /// <response code="200">The created source.</response>
    /// <response code="400">The input breaks a field rule, such as a filter that is not a filter node; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPost]
    [RequireEditable(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType<VariableSourceRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<VariableSourceRecord>> Create(string tenantId, long versionId, VariableSourceInput input, CancellationToken cancellationToken)
    {
        return Ok(await sources.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>Replaces a variable source of a draft or published model version, keeping its ID.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="source">The draft or published model version and its source.</param>
    /// <param name="input">The source's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored source.</returns>
    /// <response code="200">The stored source.</response>
    /// <response code="400">The source is not in the version, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("{itemId:long}")]
    [RequireEditable(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType<VariableSourceRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<VariableSourceRecord>> Update(
        string tenantId,
        [FromRoute] ModelItemReference<VariableSourceRecord> source,
        VariableSourceInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await sources.UpdateAsync(source.VersionId, source.ItemId, input, cancellationToken));
    }

    /// <summary>Deletes a variable source of a draft or published model version with the profile picks and override routes that name it.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="source">The draft or published model version and its source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when nothing was deleted.</returns>
    /// <response code="204">The source is deleted.</response>
    /// <response code="400">The source is not in the version; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is neither a draft nor published.</response>
    [HttpDelete("{itemId:long}")]
    [RequireEditable(VersionKinds.CalculationModelVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] ModelItemReference<VariableSourceRecord> source, CancellationToken cancellationToken)
    {
        var conflict = await sources.DeleteAsync(source.ItemId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
