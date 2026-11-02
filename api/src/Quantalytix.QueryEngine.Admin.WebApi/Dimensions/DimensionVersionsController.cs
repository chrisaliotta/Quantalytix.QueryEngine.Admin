using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Dimensions;

/// <summary>
/// Dimension versions (blueprint 12): the content of a live or frozen version, and the member and level delete checks. A report
/// dimension's versions answer 404 (M8).
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/dimension-versions/{versionId:long}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class DimensionVersionsController(DimensionQueries queries) : ControllerBase
{
    /// <summary>Reads a version with its levels, members and bands.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version content.</returns>
    /// <response code="200">The version with its levels, members and bands.</response>
    [HttpGet]
    [ProducesResponseType<DimensionVersionContent>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DimensionVersionContent>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.ContentAsync(versionId, cancellationToken));
    }

    /// <summary>Checks whether a member can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The member's version.</param>
    /// <param name="memberId">The member.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check.</response>
    [HttpGet("members/{memberId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckMemberDelete(string tenantId, long versionId, long memberId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.MemberDeleteCheckAsync(versionId, memberId, cancellationToken));
    }

    /// <summary>Checks whether a level can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="versionId">The level's version.</param>
    /// <param name="levelId">The level.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check.</response>
    [HttpGet("levels/{levelId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckLevelDelete(string tenantId, long versionId, long levelId, CancellationToken cancellationToken)
    {
        return OkOrNotFound(await queries.LevelDeleteCheckAsync(versionId, levelId, cancellationToken));
    }

    /// <summary>200 with the value, or 404 for a report dimension's.</summary>
    private ActionResult<T> OkOrNotFound<T>(T? value) where T : class
    {
        return value is null ? Problems.OfStatus(StatusCodes.Status404NotFound).ToResult() : Ok(value);
    }
}
