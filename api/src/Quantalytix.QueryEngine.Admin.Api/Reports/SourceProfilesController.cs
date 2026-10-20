using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists and edits the source profiles of report versions: the source each variable reads under the profile.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/report-versions/{versionId:long}/profiles")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class SourceProfilesController(IVersionContent<SourceProfile, SourceProfileInput> profiles) : ControllerBase
{
    /// <summary>Lists a report version's source profiles.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The source profiles by key.</returns>
    /// <response code="200">The profiles by key, ignoring case, each with its picks by variable.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SourceProfile>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SourceProfile>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await profiles.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Adds a source profile with its picks to a draft or published report version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The new profile.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created profile.</returns>
    /// <response code="200">The created profile.</response>
    /// <response code="400">The input breaks a field rule, such as a pick of another variable's source; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPost]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<SourceProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<SourceProfile>> Create(string tenantId, long versionId, SourceProfileInput input, CancellationToken cancellationToken)
    {
        return Ok(await profiles.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>Replaces a source profile of a draft or published report version and all its picks, keeping the profile's ID.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="profile">The draft or published report version and its profile.</param>
    /// <param name="input">The profile's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored profile.</returns>
    /// <response code="200">The stored profile.</response>
    /// <response code="400">The profile is not in the version, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("{itemId:long}")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<SourceProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<SourceProfile>> Update(
        string tenantId,
        [FromRoute] VersionItemReference<SourceProfile> profile,
        SourceProfileInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await profiles.UpdateAsync(profile.VersionId, profile.ItemId, input, cancellationToken));
    }

    /// <summary>Deletes a source profile of a draft or published report version with its picks, unless its report version runs under it by default.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="profile">The draft or published report version and its profile.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when nothing was deleted.</returns>
    /// <response code="204">The profile is deleted.</response>
    /// <response code="400">The profile is not in the version; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is neither a draft nor published, or the profile is its report version's default.</response>
    [HttpDelete("{itemId:long}")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] VersionItemReference<SourceProfile> profile, CancellationToken cancellationToken)
    {
        var conflict = await profiles.DeleteAsync(profile.ItemId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
