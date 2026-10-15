using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Bindings;

/// <summary>
/// Edits the dimension bindings of dataset versions: the semantic routes from fields to canonical dimensions, with the time
/// extension of time bindings.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/dataset-versions/{versionId:long}/bindings")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class BindingsController(BindingService bindings) : ControllerBase
{
    /// <summary>Lists the bindings of a dataset version, each with its time extension.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The bindings, sorted by display name.</returns>
    /// <response code="200">The bindings, sorted by display name.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BindingDetail>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<IReadOnlyList<BindingDetail>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await bindings.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Creates a binding in a draft dataset version, with its time extension when it is a time binding.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft dataset version.</param>
    /// <param name="input">The new binding.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new binding.</returns>
    /// <response code="200">The new binding.</response>
    /// <response code="400">The input breaks a rule of its binding kind, grouping mode or time extension; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPost]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<BindingDetail>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BindingDetail>> Create(string tenantId, long versionId, BindingInput input, CancellationToken cancellationToken)
    {
        return Ok(await bindings.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Replaces a binding's attributes and time extension. A binding whose kind changes away from <c>time</c> loses its
    /// time extension.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its binding.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated binding.</returns>
    /// <response code="200">The updated binding.</response>
    /// <response code="400">The binding is not in the version, or the input breaks a binding rule.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("{bindingId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<BindingDetail>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BindingDetail>> Update(
        string tenantId,
        [FromRoute] BindingReference reference,
        BindingInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await bindings.UpdateAsync(reference, input, cancellationToken));
    }

    /// <summary>
    /// Deletes a binding and its time extension, unless it is required, a bridge binding remaps it, a measure's time policy
    /// requires it, or it is the version's default time binding.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its binding.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when something blocks the delete.</returns>
    /// <response code="204">The binding and its time extension are deleted.</response>
    /// <response code="400">The binding is not in the version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft, or the detail names what blocks the delete.</response>
    [HttpDelete("{bindingId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] BindingReference reference, CancellationToken cancellationToken)
    {
        var conflict = await bindings.DeleteAsync(reference, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
