using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.WebApi.Mappings;

/// <summary>Lists, creates, edits and deletes mapping sets: the source code systems that map onto canonical dimensions.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/mapping-sets")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class MappingSetsController(MappingSetService mappingSets) : ControllerBase
{
    /// <summary>Lists the mapping sets of one dimension, which the dimension's page shows.</summary>
    /// <param name="tenantId">The tenant that owns the mapping sets.</param>
    /// <param name="dimensionId">The dimension whose codes the sets map onto.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The mapping sets by key.</returns>
    /// <response code="200">The dimension's mapping sets by key, ignoring case.</response>
    /// <response code="400">The request names no dimension.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MappingSetRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<MappingSetRecord>>> List(
        string tenantId, [FromQuery, BindRequired] long dimensionId, CancellationToken cancellationToken)
    {
        return Ok(await mappingSets.ListAsync(dimensionId, cancellationToken));
    }

    /// <summary>Creates a mapping set onto a dimension; it has no versions until one is created.</summary>
    /// <param name="tenantId">The tenant that owns the mapping set.</param>
    /// <param name="input">The new set's key, target dimension, source namespace and display name.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created mapping set.</returns>
    /// <response code="200">The created mapping set.</response>
    /// <response code="400">The input breaks a field rule, such as a taken key; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<MappingSetRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<MappingSetRecord>> Create(string tenantId, NewMappingSetInput input, CancellationToken cancellationToken)
    {
        return Ok(await mappingSets.CreateAsync(input, cancellationToken));
    }

    /// <summary>Reads one mapping set.</summary>
    /// <param name="tenantId">The tenant that owns the mapping set.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The mapping set.</returns>
    /// <response code="200">The mapping set.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{mappingSetId:long}")]
    [ProducesResponseType<MappingSetRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MappingSetRecord>> Get(string tenantId, long mappingSetId, CancellationToken cancellationToken)
    {
        return Ok(await mappingSets.GetAsync(mappingSetId, cancellationToken));
    }

    /// <summary>Edits a mapping set's source namespace and display name; a change advances the semantic revision.</summary>
    /// <param name="tenantId">The tenant that owns the mapping set.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored mapping set.</returns>
    /// <response code="200">The stored mapping set.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPut("{mappingSetId:long}")]
    [ProducesResponseType<MappingSetRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<MappingSetRecord>> Update(
        string tenantId,
        long mappingSetId,
        MappingSetInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await mappingSets.UpdateAsync(mappingSetId, input, cancellationToken));
    }

    /// <summary>
    /// Deletes a mapping set whose versions are all drafts and that no binding references, together with its drafts and
    /// their entries.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the mapping set.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when something blocks the delete.</returns>
    /// <response code="204">The mapping set and its drafts are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted; the detail names the non-draft versions and bindings that block the delete.</response>
    [HttpDelete("{mappingSetId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long mappingSetId, CancellationToken cancellationToken)
    {
        var conflict = await mappingSets.DeleteAsync(mappingSetId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
