using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Datasets;

/// <summary>Reads dataset versions and edits or deletes drafts. Status changes go through the version status endpoint.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/dataset-versions")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class DatasetVersionsController(DatasetVersionService versions) : ControllerBase
{
    /// <summary>Reads one dataset version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version.</returns>
    /// <response code="200">The version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{versionId:long}")]
    [ProducesResponseType<DatasetVersionRecord>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<DatasetVersionRecord>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await versions.GetAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces the settings of a draft dataset version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft dataset version.</param>
    /// <param name="input">The new settings.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated version.</returns>
    /// <response code="200">The updated version.</response>
    /// <response code="400">The input breaks a field rule, the source object does not exist, or the properties are not JSON.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("{versionId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<DatasetVersionRecord>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DatasetVersionRecord>> Update(
        string tenantId,
        long versionId,
        DatasetVersionSettingsInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.UpdateAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Deletes a draft dataset version that no release manifest pins, with all its content, children first: grain fields,
    /// grains, measure time policies, dependencies and components, measures, time bindings, bindings and fields.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft dataset version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when a release manifest pins the version.</returns>
    /// <response code="204">The draft and its content are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft, or a release manifest pins it; the detail names the releases.</response>
    [HttpDelete("{versionId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        var conflict = await versions.DeleteDraftAsync(versionId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
