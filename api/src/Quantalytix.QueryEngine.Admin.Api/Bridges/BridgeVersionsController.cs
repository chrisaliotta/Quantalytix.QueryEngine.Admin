using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Bridges;

/// <summary>Lists, creates, clones, edits and deletes bridge versions. Status changes go through the versions status endpoint.</summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class BridgeVersionsController(BridgeVersionService versions) : ControllerBase
{
    /// <summary>Lists a bridge's versions with their endpoint versions, allocation semantics and status.</summary>
    /// <param name="tenantId">The tenant that owns the bridge.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The versions by version number.</returns>
    /// <response code="200">The versions by version number.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("bridges/{bridgeId:long}/versions")]
    [ProducesResponseType<IReadOnlyList<BridgeVersionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BridgeVersionRecord>>> List(string tenantId, long bridgeId, CancellationToken cancellationToken)
    {
        return Ok(await versions.ListAsync(bridgeId, cancellationToken));
    }

    /// <summary>
    /// Creates a draft with the bridge's next version number: empty, or cloned from <c>cloneFromVersionId</c>, whose mappings
    /// move to the members with the same keys in the new endpoint versions.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the bridge.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="input">The endpoint versions, cardinality, allocation mode and optional clone source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created draft and the mappings the clone dropped.</returns>
    /// <response code="200">The created draft, with the cloned mappings whose member keys the new endpoint versions lack.</response>
    /// <response code="400">The input breaks a field rule, such as an unknown allocation mode; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost("bridges/{bridgeId:long}/versions")]
    [ProducesResponseType<CreatedBridgeVersion>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CreatedBridgeVersion>> Create(
        string tenantId,
        long bridgeId,
        NewBridgeVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.CreateAsync(bridgeId, input, cancellationToken));
    }

    /// <summary>Reads one bridge version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version.</returns>
    /// <response code="200">The version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("bridge-versions/{versionId:long}")]
    [ProducesResponseType<BridgeVersionRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BridgeVersionRecord>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await versions.GetAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces a draft version's endpoint versions, cardinality and allocation mode; its mappings stay as they are.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft bridge version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored version.</returns>
    /// <response code="200">The stored version.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("bridge-versions/{versionId:long}")]
    [RequireDraft(VersionKinds.BridgeVersion, "versionId")]
    [ProducesResponseType<BridgeVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BridgeVersionRecord>> Update(
        string tenantId,
        long versionId,
        BridgeVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.UpdateAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Deletes a draft version that no binding uses, with its mappings, and clears the bridge's current-version pointer when it
    /// named the version.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft bridge version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the version is not a draft or a binding uses it.</returns>
    /// <response code="204">The version and its mappings are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft, or the detail names the bindings that use it.</response>
    [HttpDelete("bridge-versions/{versionId:long}")]
    [RequireDraft(VersionKinds.BridgeVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        var conflict = await versions.DeleteAsync(versionId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
