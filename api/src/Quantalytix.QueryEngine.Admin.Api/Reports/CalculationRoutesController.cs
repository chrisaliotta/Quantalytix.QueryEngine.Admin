using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists and edits the routes of report versions: how a dataset, by default or for one source, produces a calculation dimension.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/report-versions/{versionId:long}/routes")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class CalculationRoutesController(IVersionContent<CalculationRouteRecord, CalculationRouteInput> routes) : ControllerBase
{
    /// <summary>Lists a report version's calculation routes.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The calculation routes by key.</returns>
    /// <response code="200">The routes by field key, ignoring case.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CalculationRouteRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalculationRouteRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await routes.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Adds a route to a draft or published report version; whether its field produces the dimension in the dataset version a release pins is a release rule.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The new route.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created route.</returns>
    /// <response code="200">The created route.</response>
    /// <response code="400">The input breaks a field rule, such as a second default route for the dimension and dataset; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPost]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<CalculationRouteRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculationRouteRecord>> Create(string tenantId, long versionId, CalculationRouteInput input, CancellationToken cancellationToken)
    {
        return Ok(await routes.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>Replaces a route of a draft or published report version, keeping its ID.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="route">The draft or published report version and its route.</param>
    /// <param name="input">The route's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored route.</returns>
    /// <response code="200">The stored route.</response>
    /// <response code="400">The route is not in the version, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("{itemId:long}")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<CalculationRouteRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CalculationRouteRecord>> Update(
        string tenantId,
        [FromRoute] VersionItemReference<CalculationRouteRecord> route,
        CalculationRouteInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await routes.UpdateAsync(route.VersionId, route.ItemId, input, cancellationToken));
    }

    /// <summary>Deletes a route of a draft or published report version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="route">The draft or published report version and its route.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when nothing was deleted.</returns>
    /// <response code="204">The route is deleted.</response>
    /// <response code="400">The route is not in the version; the error is keyed <c>itemId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">Nothing was deleted: the version is neither a draft nor published.</response>
    [HttpDelete("{itemId:long}")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] VersionItemReference<CalculationRouteRecord> route, CancellationToken cancellationToken)
    {
        var conflict = await routes.DeleteAsync(route.ItemId, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }
}
