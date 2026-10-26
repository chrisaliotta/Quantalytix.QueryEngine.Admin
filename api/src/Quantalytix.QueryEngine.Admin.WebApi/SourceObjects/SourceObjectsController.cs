using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Api.SourceObjects;

/// <summary>Lists the tables and views of the tenant database that datasets can query, and their columns.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/source-objects")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class SourceObjectsController(SourceObjectService sourceObjects) : ControllerBase
{
    /// <summary>Lists the tables and views datasets can query; the query engine's <c>qe_*</c> and <c>vw_qe_*</c> objects are left out.</summary>
    /// <param name="tenantId">The tenant whose database is read.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The tables and views, sorted by name.</returns>
    /// <response code="200">The tables and views, sorted by name.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SourceObject>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SourceObject>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await sourceObjects.ListAsync(cancellationToken));
    }

    /// <summary>Lists the columns of a table or view with the field type each maps to.</summary>
    /// <param name="tenantId">The tenant whose database is read.</param>
    /// <param name="name">The table or view name.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The columns, in ordinal order.</returns>
    /// <response code="200">The columns in ordinal order; empty for a name the database does not have.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("{name}/columns")]
    [ProducesResponseType<IReadOnlyList<SourceColumn>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SourceColumn>>> ListColumns(string tenantId, string name, CancellationToken cancellationToken)
    {
        return Ok(await sourceObjects.ListColumnsAsync(name, cancellationToken));
    }
}
