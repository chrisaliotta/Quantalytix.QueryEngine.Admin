using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Api.AssumptionSets;

/// <summary>
/// Lists and shows assumption sets, captures historical sets from the active set, relabels sets and deletes historical ones. The
/// active set pins every published version and every publish repoints it; a query or report request names a historical set to
/// rerun the versions it captured over the release that was active when it was captured.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/assumption-sets")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class AssumptionSetsController(AssumptionSetService sets) : ControllerBase
{
    /// <summary>Lists the tenant's assumption sets: the active one, then historical sets, most recently captured first.</summary>
    /// <param name="tenantId">The tenant that owns the assumption sets.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The assumption sets.</returns>
    /// <response code="200">The assumption sets.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AssumptionSetRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssumptionSetRecord>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await sets.ListAsync(cancellationToken));
    }

    /// <summary>Captures a historical set: a copy of the active set's current pins under a new key, reading the active release.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="input">The key, display name and description.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new historical set.</returns>
    /// <response code="201">The set was captured; the body is the new historical set.</response>
    /// <response code="400">The key is malformed or taken, or the display name is missing.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<AssumptionSetRecord>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<AssumptionSetRecord>> Capture(string tenantId, AssumptionCaptureInput input, CancellationToken cancellationToken)
    {
        var set = await sets.CaptureAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { tenantId, assumptionSetId = set.AssumptionSetId }, set);
    }

    /// <summary>Reads one assumption set with its pins, each labelled with its identity's key and its version's number and status.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The set and its pins.</returns>
    /// <response code="200">The set and its pins.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{assumptionSetId:long}")]
    [ProducesResponseType<AssumptionSetDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssumptionSetDetail>> Get(string tenantId, long assumptionSetId, CancellationToken cancellationToken)
    {
        return Ok(await sets.GetAsync(assumptionSetId, cancellationToken));
    }

    /// <summary>Changes a set's display name and description; its key and pins never change.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="input">The new labels.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated set.</returns>
    /// <response code="200">The updated set.</response>
    /// <response code="400">The display name is missing, or a label is too long.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPut("{assumptionSetId:long}")]
    [ProducesResponseType<AssumptionSetRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<AssumptionSetRecord>> Update(
        string tenantId,
        long assumptionSetId,
        AssumptionSetUpdateInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await sets.UpdateAsync(assumptionSetId, input, cancellationToken));
    }

    /// <summary>Deletes a historical set with its pins; requests can no longer name its key.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="assumptionSetId">The historical set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the set is the active one.</returns>
    /// <response code="204">The set and its pins are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the set is the active set, which holds the default versions.</response>
    [HttpDelete("{assumptionSetId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long assumptionSetId, CancellationToken cancellationToken)
    {
        var conflict = await sets.DeleteAsync(assumptionSetId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
