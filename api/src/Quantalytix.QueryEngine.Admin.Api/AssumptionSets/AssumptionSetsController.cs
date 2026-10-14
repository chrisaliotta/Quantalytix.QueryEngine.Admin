using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.AssumptionSets;

/// <summary>
/// Creates assumption sets and edits the exact dimension versions they pin. Publishing and retiring a set go through the
/// version status endpoint with kind <c>assumption_set</c>; sets have no validated state.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/assumption-sets")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class AssumptionSetsController(AssumptionSetService sets) : ControllerBase
{
    /// <summary>Lists the tenant's assumption sets, ordered by key.</summary>
    /// <param name="tenantId">The tenant that owns the assumption sets.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The assumption sets.</returns>
    /// <response code="200">The assumption sets, ordered by key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AssumptionSetRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssumptionSetRecord>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await sets.ListAsync(cancellationToken));
    }

    /// <summary>Creates a draft assumption set without pins.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="input">The key, display name, description and effective time (UTC).</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new draft assumption set.</returns>
    /// <response code="201">The assumption set was created; the body is the new draft.</response>
    /// <response code="400">The key is malformed or taken, the display name is missing, or the effective time is not UTC.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<AssumptionSetRecord>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<AssumptionSetRecord>> Create(string tenantId, AssumptionSetCreateInput input, CancellationToken cancellationToken)
    {
        var set = await sets.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { tenantId, assumptionSetId = set.AssumptionSetId }, set);
    }

    /// <summary>Reads one assumption set.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The assumption set.</returns>
    /// <response code="200">The assumption set.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{assumptionSetId:long}")]
    [ProducesResponseType<AssumptionSetRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssumptionSetRecord>> Get(string tenantId, long assumptionSetId, CancellationToken cancellationToken)
    {
        return Ok(await sets.GetAsync(assumptionSetId, cancellationToken));
    }

    /// <summary>Changes a draft assumption set's display name, description and effective time.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="assumptionSetId">The draft assumption set.</param>
    /// <param name="input">The new settings; the key is fixed.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated assumption set.</returns>
    /// <response code="200">The updated assumption set.</response>
    /// <response code="400">The display name is missing or too long, or the effective time is not UTC.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The assumption set is not a draft.</response>
    [HttpPut("{assumptionSetId:long}")]
    [RequireDraft(VersionKinds.AssumptionSet, "assumptionSetId")]
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

    /// <summary>Deletes a draft assumption set and its pins.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="assumptionSetId">The draft assumption set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The assumption set and its pins are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The assumption set is not a draft.</response>
    [HttpDelete("{assumptionSetId:long}")]
    [RequireDraft(VersionKinds.AssumptionSet, "assumptionSetId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long assumptionSetId, CancellationToken cancellationToken)
    {
        await sets.DeleteAsync(assumptionSetId, cancellationToken);
        return NoContent();
    }

    /// <summary>Lists an assumption set's pins: the dimension version it captures for each dimension.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The pins, ordered by dimension.</returns>
    /// <response code="200">The pins, ordered by dimension.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{assumptionSetId:long}/dimensions")]
    [ProducesResponseType<IReadOnlyList<AssumptionSetDimensionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssumptionSetDimensionRecord>>> ListDimensions(
        string tenantId,
        long assumptionSetId,
        CancellationToken cancellationToken)
    {
        return Ok(await sets.ListPinsAsync(assumptionSetId, cancellationToken));
    }

    /// <summary>Replaces a draft assumption set's pins.</summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="assumptionSetId">The draft assumption set.</param>
    /// <param name="input">The complete list of pins, one per dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored pins.</returns>
    /// <response code="200">The stored pins, ordered by dimension.</response>
    /// <response code="400">A dimension does not exist or appears more than once.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The assumption set is not a draft.</response>
    [HttpPut("{assumptionSetId:long}/dimensions")]
    [RequireDraft(VersionKinds.AssumptionSet, "assumptionSetId")]
    [ProducesResponseType<IReadOnlyList<AssumptionSetDimensionRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<AssumptionSetDimensionRecord>>> ReplaceDimensions(
        string tenantId,
        long assumptionSetId,
        AssumptionPinsInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await sets.ReplacePinsAsync(assumptionSetId, input, cancellationToken));
    }

    /// <summary>
    /// Pins, for each given dimension, its published version effective at the set's <c>effective_at</c>
    /// (<c>valid_from &lt;= effective_at &lt; valid_to</c>, a null <c>valid_to</c> being open-ended), replacing the pins of
    /// those dimensions. A dimension without such a version is left unpinned.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the assumption set.</param>
    /// <param name="assumptionSetId">The draft assumption set.</param>
    /// <param name="input">The dimensions to capture.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>All of the set's pins after the capture.</returns>
    /// <response code="200">All of the set's pins after the capture, ordered by dimension.</response>
    /// <response code="400">No dimension is named, or a dimension does not exist.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The assumption set is not a draft.</response>
    [HttpPost("{assumptionSetId:long}/capture")]
    [RequireDraft(VersionKinds.AssumptionSet, "assumptionSetId")]
    [ProducesResponseType<IReadOnlyList<AssumptionSetDimensionRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<AssumptionSetDimensionRecord>>> Capture(
        string tenantId,
        long assumptionSetId,
        AssumptionCaptureInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await sets.CaptureAsync(assumptionSetId, input, cancellationToken));
    }
}
