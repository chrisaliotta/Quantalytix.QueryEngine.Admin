using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.SourceObjects;

/// <summary>The tables and views of the tenant database a dataset version can read, with their columns (blueprint 11).</summary>
[ApiController]
[Route("api/tenants/{tenantId}/source-objects")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<Problem>(StatusCodes.Status401Unauthorized, ProblemWriter.ContentType)]
[ProducesResponseType<Problem>(StatusCodes.Status403Forbidden, ProblemWriter.ContentType)]
public sealed class SourceObjectsController(SourceObjectDirectory sourceObjects) : ControllerBase
{
    /// <summary>Lists the tables and views with whether the API manages each and their live statistics.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The objects by name.</returns>
    /// <response code="200">Every table and view datasets can read.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DatasetSourceObject>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DatasetSourceObject>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await sourceObjects.ListAsync(cancellationToken));
    }

    /// <summary>Lists the columns of a table or view with the field type each reads as.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="kind">A <c>source_object_kind</c> value.</param>
    /// <param name="name">The table or view.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The columns in ordinal order.</returns>
    /// <response code="200">The columns.</response>
    /// <response code="404">No table or view of that kind and name.</response>
    [HttpGet("{kind}/{name}/columns")]
    [ProducesResponseType<IReadOnlyList<DatasetSourceColumn>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status404NotFound, ProblemWriter.ContentType)]
    public async Task<ActionResult<IReadOnlyList<DatasetSourceColumn>>> Columns(string tenantId, string kind, string name, CancellationToken cancellationToken)
    {
        return await sourceObjects.ColumnsAsync(kind, name, cancellationToken) is { } columns ? Ok(columns) : Problems.OfStatus(StatusCodes.Status404NotFound).ToResult();
    }
}
