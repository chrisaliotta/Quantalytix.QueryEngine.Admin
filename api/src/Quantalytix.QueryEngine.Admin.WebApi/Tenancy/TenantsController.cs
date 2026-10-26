using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Api.Tenancy;

/// <summary>Lists the tenants the caller may administer.</summary>
[ApiController]
[Route("api/tenants")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class TenantsController(AccessibleTenantsService tenants, CallerFactory callers) : ControllerBase
{
    /// <summary>Lists every tenant for a SystemAdmin, otherwise only the caller's own tenant. Connection strings are never included.</summary>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The accessible tenants, sorted by tenant ID.</returns>
    /// <response code="200">The accessible tenants, sorted by tenant ID.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccessibleTenant>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccessibleTenant>>> List(CancellationToken cancellationToken)
    {
        return Ok(await tenants.ListAsync(callers.Create(User), cancellationToken));
    }
}
