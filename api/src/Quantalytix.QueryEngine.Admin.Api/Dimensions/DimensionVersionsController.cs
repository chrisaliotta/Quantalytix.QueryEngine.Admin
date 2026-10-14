using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Dimensions;

/// <summary>Lists, creates, clones, edits and deletes dimension versions. Status changes go through the versions status endpoint.</summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class DimensionVersionsController(DimensionVersionService versions) : ControllerBase
{
    /// <summary>Lists a dimension's versions with their status and validity windows, from which the effective version follows.</summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The versions by version number.</returns>
    /// <response code="200">The versions by version number, each with <c>validFrom</c> and <c>validTo</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("dimensions/{dimensionId:long}/versions")]
    [ProducesResponseType<IReadOnlyList<DimensionVersionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DimensionVersionRecord>>> List(
        string tenantId,
        long dimensionId,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.ListAsync(dimensionId, cancellationToken));
    }

    /// <summary>
    /// Creates a draft with the dimension's next version number, empty or cloned from <c>cloneFromVersionId</c>. A clone copies
    /// the levels, members, range bands and the dimension's mapping versions pinned to the source, and the source's range data
    /// type unless one is supplied.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="input">The new version's range data type, validity window and optional clone source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created draft version.</returns>
    /// <response code="200">The created draft version.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost("dimensions/{dimensionId:long}/versions")]
    [ProducesResponseType<DimensionVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionVersionRecord>> Create(
        string tenantId,
        long dimensionId,
        NewDimensionVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.CreateAsync(dimensionId, input, cancellationToken));
    }

    /// <summary>Reads one dimension version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimensionVersionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version.</returns>
    /// <response code="200">The version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("dimension-versions/{dimensionVersionId:long}")]
    [ProducesResponseType<DimensionVersionRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DimensionVersionRecord>> Get(string tenantId, long dimensionVersionId, CancellationToken cancellationToken)
    {
        return Ok(await versions.GetAsync(dimensionVersionId, cancellationToken));
    }

    /// <summary>Replaces a draft version's range data type and validity window.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimensionVersionId">The draft version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored version.</returns>
    /// <response code="200">The stored version.</response>
    /// <response code="400">The input breaks a field rule, such as <c>validTo</c> not after <c>validFrom</c>; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("dimension-versions/{dimensionVersionId:long}")]
    [RequireDraft(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType<DimensionVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionVersionRecord>> Update(
        string tenantId,
        long dimensionVersionId,
        DimensionVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.UpdateAsync(dimensionVersionId, input, cancellationToken));
    }

    /// <summary>
    /// Deletes a draft version with its levels, members, range bands, closure rows and the draft mapping versions pinned to it,
    /// unless a non-draft mapping version, a bridge version, a binding or an assumption set pins it.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimensionVersionId">The draft version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the version is not a draft or something pins it.</returns>
    /// <response code="204">The version and its content are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft, or the detail names what pins it.</response>
    [HttpDelete("dimension-versions/{dimensionVersionId:long}")]
    [RequireDraft(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long dimensionVersionId, CancellationToken cancellationToken)
    {
        var conflict = await versions.DeleteAsync(dimensionVersionId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
