using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Dimensions;

/// <summary>Lists and edits the members of dimension versions, including drag-and-drop reparenting and reordering.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/dimension-versions/{dimensionVersionId:long}/members")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class DimensionMembersController(DimensionMemberService members) : ControllerBase
{
    /// <summary>Lists every member of a version; parent links make the tree.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimensionVersionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The members by sort order.</returns>
    /// <response code="200">The members by sort order, then key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DimensionMemberRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DimensionMemberRecord>>> List(
        string tenantId,
        long dimensionVersionId,
        CancellationToken cancellationToken)
    {
        return Ok(await members.ListAsync(dimensionVersionId, cancellationToken));
    }

    /// <summary>Adds a member to a draft version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimensionVersionId">The draft version.</param>
    /// <param name="input">The new member.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created member.</returns>
    /// <response code="200">The created member.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPost]
    [RequireDraft(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType<DimensionMemberRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionMemberRecord>> Create(
        string tenantId,
        long dimensionVersionId,
        NewMemberInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await members.CreateAsync(dimensionVersionId, input, cancellationToken));
    }

    /// <summary>Replaces a member's editable attributes, including its parent and level; its key stays.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="member">The draft version and its member.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored member.</returns>
    /// <response code="200">The stored member.</response>
    /// <response code="400">The member is not in the version, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("{memberId:long}")]
    [RequireDraft(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType<DimensionMemberRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionMemberRecord>> Update(
        string tenantId,
        [FromRoute] MemberReference member,
        MemberInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await members.UpdateAsync(member, input, cancellationToken));
    }

    /// <summary>Stores a drag-and-drop reparent or reorder in one call: one update per member whose parent or sort order changed.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="dimensionVersionId">The draft version.</param>
    /// <param name="input">The members' new parents and sort orders.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version's members after the change.</returns>
    /// <response code="200">The version's members after the change, by sort order, then key.</response>
    /// <response code="400">A member is placed twice or under itself; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("order")]
    [RequireDraft(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType<IReadOnlyList<DimensionMemberRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<DimensionMemberRecord>>> Reorder(
        string tenantId,
        long dimensionVersionId,
        MemberOrderInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await members.ReorderAsync(dimensionVersionId, input, cancellationToken));
    }

    /// <summary>Deletes a member of a draft version, unless it has children or a mapping entry, range band or bridge mapping references it.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="member">The draft version and its member.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the version is not a draft or something references the member.</returns>
    /// <response code="204">The member is deleted.</response>
    /// <response code="400">The member is not in the version; the error is keyed <c>memberId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft, or the detail names what references the member.</response>
    [HttpDelete("{memberId:long}")]
    [RequireDraft(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] MemberReference member, CancellationToken cancellationToken)
    {
        var conflict = await members.DeleteAsync(member, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
