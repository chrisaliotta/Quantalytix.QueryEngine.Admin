using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Binding;
using Quantalytix.QueryEngine.Admin.Application.Lookups;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Lookups;

/// <summary>Serves the picker lists of the web app, one <c>lookup_kind</c> at a time.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/lookups")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class LookupsController(LookupService lookups) : ControllerBase
{
    /// <summary>Lists the items of one picker, such as the versions of a dimension.</summary>
    /// <param name="tenantId">The tenant whose metadata is listed.</param>
    /// <param name="query">
    /// The lookup kind (path), for child kinds the parent row's ID (query <c>parentId</c>), and for <c>dimensions</c> an optional
    /// <c>dimension_purpose</c> (query <c>purpose</c>).
    /// </param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The picker's items.</returns>
    /// <response code="200">The picker's items, sorted by key unless the kind has a natural order (versions, levels, members).</response>
    /// <response code="400">The kind is not a <c>lookup_kind</c> value, a child kind has no <c>parentId</c>, or the purpose is unknown or given for another kind.</response>
    [HttpGet("{kind}")]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> List(
        string tenantId,
        [FromRouteAndQuery] LookupQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await lookups.ListAsync(query, cancellationToken));
    }
}
