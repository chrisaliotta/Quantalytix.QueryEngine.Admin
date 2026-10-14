using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Binding;
using Quantalytix.QueryEngine.Admin.Application.Lookups;

namespace Quantalytix.QueryEngine.Admin.Api.Lookups;

/// <summary>Serves the picker lists of the web app, one <c>lookup_kind</c> at a time.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/lookups")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class LookupsController(LookupService lookups) : ControllerBase
{
    /// <summary>Lists the items of one picker, such as the versions of a dimension.</summary>
    /// <param name="tenantId">The tenant whose metadata is listed.</param>
    /// <param name="query">The lookup kind (path) and, for child kinds, the parent row's ID (query <c>parentId</c>).</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The picker's items.</returns>
    /// <response code="200">The picker's items, sorted by key unless the kind has a natural order (versions, levels, members).</response>
    /// <response code="400">The kind is not a <c>lookup_kind</c> value, or a child kind has no <c>parentId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{kind}")]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> List(
        string tenantId,
        [FromRouteAndQuery] LookupQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await lookups.ListAsync(query.Kind, query.ParentId, cancellationToken));
    }
}
