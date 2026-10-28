using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.AssumptionSets;

/// <summary>
/// Assumption sets (blueprint 14): the active set and the historical sets, a set with its pins and readiness, the comparison of two
/// sets, and capturing, relabelling and deleting historical sets. The active set pins every published version and every publish
/// repoints it; a request names a historical set to rerun the versions it captured over the release it captured.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/assumption-sets")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class AssumptionSetsController(AssumptionSetQueries queries, AssumptionSetCommands commands) : ControllerBase
{
    /// <summary>Lists the active set and the historical sets with their pin counts.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The sets.</returns>
    /// <response code="200">The active set and every historical set, newest capture first.</response>
    [HttpGet]
    [ProducesResponseType<AssumptionSetList>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssumptionSetList>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ListAsync(cancellationToken));
    }

    /// <summary>Captures the active set's analytical pins and the active release into a historical set; advances the semantic revision.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The key and labels.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new set.</returns>
    /// <response code="201">The new historical set.</response>
    /// <response code="400">The key is malformed or a label is missing or too long.</response>
    /// <response code="409">The key is taken.</response>
    [HttpPost]
    [ProducesResponseType<AssumptionSet>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<AssumptionSet>> Capture(string tenantId, CaptureSetInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.CaptureAsync(input, cancellationToken);
        if (!outcome.Succeeded) return Problems.From(outcome.Refusal).ToResult();
        return CreatedAtAction(nameof(Get), new { tenantId, setId = outcome.Value.Id }, outcome.Value);
    }

    /// <summary>Counts what a capture would pin and leave out.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The preview.</returns>
    /// <response code="200">The counts and the release a capture would record.</response>
    [HttpGet("capture-preview")]
    [ProducesResponseType<SetCapturePreview>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SetCapturePreview>> CapturePreview(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await queries.CapturePreviewAsync(cancellationToken));
    }

    /// <summary>Compares the pins and the releases of two sets.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="from">The set compared from.</param>
    /// <param name="to">The set compared to.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The comparison.</returns>
    /// <response code="200">What <c>to</c> pins differently from <c>from</c>, and how their releases differ.</response>
    /// <response code="404"><c>from</c> or <c>to</c> names no assumption set.</response>
    [HttpGet("compare")]
    [ProducesResponseType<SetComparison>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status404NotFound, ProblemWriter.ContentType)]
    public async Task<ActionResult<SetComparison>> Compare(
        string tenantId,
        [FromQuery, BindRequired] long from,
        [FromQuery, BindRequired] long to,
        CancellationToken cancellationToken)
    {
        return await queries.CompareAsync(from, to, cancellationToken) is { } comparison ? Ok(comparison) : Problems.OfStatus(StatusCodes.Status404NotFound).ToResult();
    }

    /// <summary>Reads a set with its pins and, for a historical set, per-report readiness.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="setId">The set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The set.</returns>
    /// <response code="200">The set.</response>
    [HttpGet("{setId:long}")]
    [ProducesResponseType<SetDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SetDetail>> Get(string tenantId, long setId, CancellationToken cancellationToken)
    {
        return Ok(await queries.GetAsync(setId, cancellationToken));
    }

    /// <summary>Changes a historical set's display name and description; the key, pins and release never change.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="setId">The historical set.</param>
    /// <param name="input">The new labels.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The relabelled set.</returns>
    /// <response code="200">The relabelled set.</response>
    /// <response code="400">A label is missing or too long.</response>
    /// <response code="409">The active set.</response>
    [HttpPut("{setId:long}")]
    [ProducesResponseType<AssumptionSet>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<AssumptionSet>> Relabel(string tenantId, long setId, RelabelSetInput input, CancellationToken cancellationToken)
    {
        return OkOrProblem(await commands.RelabelAsync(setId, input, cancellationToken));
    }

    /// <summary>Deletes a historical set and its pins; advances the semantic revision.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="setId">The historical set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">The active set.</response>
    [HttpDelete("{setId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long setId, CancellationToken cancellationToken)
    {
        return await commands.DeleteAsync(setId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>200 with the value, or the refusal's problem.</summary>
    private ActionResult<T> OkOrProblem<T>(Outcome<T> outcome) where T : class
    {
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }
}
