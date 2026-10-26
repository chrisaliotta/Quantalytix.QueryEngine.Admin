using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.WebApi.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.WebApi.Measures;

/// <summary>
/// Edits the measures of dataset versions with their components and time policies; formula dependencies are derived from
/// the formulas on every save.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/dataset-versions/{versionId:long}/measures")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class MeasuresController(MeasureService measures) : ControllerBase
{
    /// <summary>Lists the measures of a dataset version with their components, formula dependencies and time policies.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The measures, sorted by key.</returns>
    /// <response code="200">The measures, sorted by key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MeasureDetail>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MeasureDetail>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await measures.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Creates a measure in a draft dataset version, then derives the version's formula dependencies.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft dataset version.</param>
    /// <param name="input">The new measure with its components and optional time policy.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new measure.</returns>
    /// <response code="200">The new measure, with the measures its formula depends on.</response>
    /// <response code="400">The input breaks a rule of its measure kind, output type or time policy; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPost]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<MeasureDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<MeasureDetail>> Create(string tenantId, long versionId, MeasureInput input, CancellationToken cancellationToken)
    {
        return Ok(await measures.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Replaces a measure's attributes, components and time policy, then derives the version's formula dependencies again;
    /// the key is fixed.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its measure.</param>
    /// <param name="input">The new attributes, components and optional time policy.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated measure.</returns>
    /// <response code="200">The updated measure, with the measures its formula depends on.</response>
    /// <response code="400">The measure is not in the version, or the input breaks a measure rule.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("{measureId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<MeasureDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<MeasureDetail>> Update(
        string tenantId,
        [FromRoute] MeasureReference reference,
        MeasureUpdateInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await measures.UpdateAsync(reference, input, cancellationToken));
    }

    /// <summary>Deletes a measure with its components, dependencies and time policy, unless another formula depends on it.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its measure.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when another formula depends on the measure.</returns>
    /// <response code="204">The measure and its child rows are deleted.</response>
    /// <response code="400">The measure is not in the version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft, or the detail names the formulas that depend on the measure.</response>
    [HttpDelete("{measureId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] MeasureReference reference, CancellationToken cancellationToken)
    {
        var conflict = await measures.DeleteAsync(reference, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
