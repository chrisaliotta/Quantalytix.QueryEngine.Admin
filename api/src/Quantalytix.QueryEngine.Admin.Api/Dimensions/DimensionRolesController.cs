using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Api.Dimensions;

/// <summary>
/// Lists, creates, edits and deletes the roles of a dimension: the named uses of the dimension that bindings, calculation
/// dimensions and bridge sources name, and whose keys requests use. A time dimension has no roles.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/dimensions/{dimensionId:long}/roles")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class DimensionRolesController(DimensionRoleService roles) : ControllerBase
{
    /// <summary>Lists the roles of a dimension.</summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The roles by key.</returns>
    /// <response code="200">The roles by key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DimensionRoleRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DimensionRoleRecord>>> List(string tenantId, long dimensionId, CancellationToken cancellationToken)
    {
        return Ok(await roles.ListAsync(dimensionId, cancellationToken));
    }

    /// <summary>Creates a role of a dimension.</summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="dimensionId">The dimension, the role's template.</param>
    /// <param name="input">The new role's key, display name and description.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created role.</returns>
    /// <response code="200">The created role.</response>
    /// <response code="400">The input breaks a field rule, such as a taken key; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<DimensionRoleRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionRoleRecord>> Create(
        string tenantId,
        long dimensionId,
        NewDimensionRoleInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await roles.CreateAsync(dimensionId, input, cancellationToken));
    }

    /// <summary>Edits a role's display name and description; a change advances the semantic revision. The key stays fixed.</summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="dimensionId">The role's dimension.</param>
    /// <param name="roleId">The role.</param>
    /// <param name="input">The new display name and description.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored role.</returns>
    /// <response code="200">The stored role.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPut("{roleId:long}")]
    [ProducesResponseType<DimensionRoleRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionRoleRecord>> Update(
        string tenantId,
        long dimensionId,
        long roleId,
        DimensionRoleInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await roles.UpdateAsync(roleId, input, cancellationToken));
    }

    /// <summary>Deletes a role that no binding, calculation dimension or variable source names.</summary>
    /// <param name="tenantId">The tenant that owns the dimension.</param>
    /// <param name="dimensionId">The role's dimension.</param>
    /// <param name="roleId">The role.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when something names the role.</returns>
    /// <response code="204">The role is deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted; the detail names the bindings, calculation dimensions and variable sources that name the role.</response>
    [HttpDelete("{roleId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, long dimensionId, long roleId, CancellationToken cancellationToken)
    {
        var conflict = await roles.DeleteAsync(roleId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
