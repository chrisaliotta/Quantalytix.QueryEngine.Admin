using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Api.Dimensions;

/// <summary>Lists, creates, edits and deletes canonical dimensions.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/dimensions")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class DimensionsController(DimensionService dimensions) : ControllerBase
{
    /// <summary>Lists every dimension of the tenant.</summary>
    /// <param name="tenantId">The tenant that owns the dimensions.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The dimensions by key.</returns>
    /// <response code="200">The dimensions by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DimensionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DimensionRecord>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await dimensions.ListAsync(cancellationToken));
    }

    /// <summary>Creates a user-defined dimension and its draft version 1.</summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="input">The new dimension's key, kind and identity, plus version 1's range data type.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created dimension.</returns>
    /// <response code="200">The created dimension; its draft version 1 is listed under its versions.</response>
    /// <response code="400">The input breaks a field rule, such as a taken key; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<DimensionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionRecord>> Create(string tenantId, NewDimensionInput input, CancellationToken cancellationToken)
    {
        return Ok(await dimensions.CreateAsync(input, cancellationToken));
    }

    /// <summary>Reads one dimension.</summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The dimension.</returns>
    /// <response code="200">The dimension.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{dimensionId:long}")]
    [ProducesResponseType<DimensionRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DimensionRecord>> Get(string tenantId, long dimensionId, CancellationToken cancellationToken)
    {
        return Ok(await dimensions.GetAsync(dimensionId, cancellationToken));
    }

    /// <summary>
    /// Edits a dimension's display name, availability and removability. A system dimension keeps its removability. A changed
    /// display name or availability advances the semantic revision.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored dimension.</returns>
    /// <response code="200">The stored dimension.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPut("{dimensionId:long}")]
    [ProducesResponseType<DimensionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionRecord>> Update(
        string tenantId,
        long dimensionId,
        DimensionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await dimensions.UpdateAsync(dimensionId, input, cancellationToken));
    }

    /// <summary>
    /// Deletes a removable dimension whose versions are all drafts and that no binding, mapping set, bridge or assumption set
    /// references, together with its drafts and their content.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when something blocks the delete.</returns>
    /// <response code="204">The dimension and its drafts are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted; the detail names what blocks the delete.</response>
    [HttpDelete("{dimensionId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long dimensionId, CancellationToken cancellationToken)
    {
        var conflict = await dimensions.DeleteAsync(dimensionId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
