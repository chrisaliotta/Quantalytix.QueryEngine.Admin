using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Api.Bridges;

/// <summary>Lists, creates, edits and deletes bridges: the semantic remappings from one canonical dimension to another.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/bridges")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class BridgesController(BridgeService bridges) : ControllerBase
{
    /// <summary>Lists the bridges of the tenant but those into a report dimension, which belong to their report's design.</summary>
    /// <param name="tenantId">The tenant that owns the bridges.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The bridges by key.</returns>
    /// <response code="200">The bridges by key, ignoring case; a report's own bridges are left out.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BridgeRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BridgeRecord>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await bridges.ListAsync(cancellationToken));
    }

    /// <summary>Creates a bridge between two dimensions; it has no versions until one is created.</summary>
    /// <param name="tenantId">The tenant that owns the bridge.</param>
    /// <param name="input">The new bridge's key, endpoint dimensions, display name and availability.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created bridge.</returns>
    /// <response code="200">The created bridge.</response>
    /// <response code="400">The input breaks a field rule, such as a taken key; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<BridgeRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BridgeRecord>> Create(string tenantId, NewBridgeInput input, CancellationToken cancellationToken)
    {
        return Ok(await bridges.CreateAsync(input, cancellationToken));
    }

    /// <summary>Reads one bridge.</summary>
    /// <param name="tenantId">The tenant that owns the bridge.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The bridge.</returns>
    /// <response code="200">The bridge.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{bridgeId:long}")]
    [ProducesResponseType<BridgeRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BridgeRecord>> Get(string tenantId, long bridgeId, CancellationToken cancellationToken)
    {
        return Ok(await bridges.GetAsync(bridgeId, cancellationToken));
    }

    /// <summary>Edits a bridge's display name and availability; a change advances the semantic revision.</summary>
    /// <param name="tenantId">The tenant that owns the bridge.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored bridge.</returns>
    /// <response code="200">The stored bridge.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPut("{bridgeId:long}")]
    [ProducesResponseType<BridgeRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BridgeRecord>> Update(string tenantId, long bridgeId, BridgeInput input, CancellationToken cancellationToken)
    {
        return Ok(await bridges.UpdateAsync(bridgeId, input, cancellationToken));
    }

    /// <summary>
    /// Deletes a bridge whose versions are all drafts and that no binding uses, together with its drafts and their mappings.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the bridge.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when something blocks the delete.</returns>
    /// <response code="204">The bridge and its drafts are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted; the detail names the non-draft versions and bindings that block the delete.</response>
    [HttpDelete("{bridgeId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long bridgeId, CancellationToken cancellationToken)
    {
        var conflict = await bridges.DeleteAsync(bridgeId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
