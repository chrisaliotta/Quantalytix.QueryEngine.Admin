using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Reports;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Dimensions;

/// <summary>Lists and edits the levels of dimension versions.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/dimension-versions/{dimensionVersionId:long}/levels")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class DimensionLevelsController(DimensionLevelService levels) : ControllerBase
{
    /// <summary>Lists a version's levels.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimensionVersionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The levels by ordinal.</returns>
    /// <response code="200">The levels by ordinal, then key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DimensionLevelRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DimensionLevelRecord>>> List(
        string tenantId,
        long dimensionVersionId,
        CancellationToken cancellationToken)
    {
        return Ok(await levels.ListAsync(dimensionVersionId, cancellationToken));
    }

    /// <summary>Adds a level to a draft or published version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimensionVersionId">The draft or published version.</param>
    /// <param name="input">The new level.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created level.</returns>
    /// <response code="200">The created level.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPost]
    [RequireEditable(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [RequireAnalytical(AnalyticalTarget.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType<DimensionLevelRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionLevelRecord>> Create(
        string tenantId,
        long dimensionVersionId,
        NewLevelInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await levels.CreateAsync(dimensionVersionId, input, cancellationToken));
    }

    /// <summary>Replaces a level's display name, ordinal, periodicity and leaf flag; its key stays.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="level">The draft or published version and its level.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored level.</returns>
    /// <response code="200">The stored level.</response>
    /// <response code="400">The level is not in the version, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("{dimensionLevelId:long}")]
    [RequireEditable(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [RequireAnalytical(AnalyticalTarget.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType<DimensionLevelRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionLevelRecord>> Update(
        string tenantId,
        [FromRoute] LevelReference level,
        LevelInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await levels.UpdateAsync(level, input, cancellationToken));
    }

    /// <summary>Deletes a level of a draft or published version, unless members use it or, in the published version, live bindings group at its key.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="level">The draft or published version and its level.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the version is neither a draft nor published or something uses the level.</returns>
    /// <response code="204">The level is deleted.</response>
    /// <response code="400">The level is not in the version; the error is keyed <c>dimensionLevelId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is neither a draft nor published, or the detail names what uses the level.</response>
    [HttpDelete("{dimensionLevelId:long}")]
    [RequireEditable(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [RequireAnalytical(AnalyticalTarget.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] LevelReference level, CancellationToken cancellationToken)
    {
        var conflict = await levels.DeleteAsync(level, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
