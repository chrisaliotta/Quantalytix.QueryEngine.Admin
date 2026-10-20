using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>
/// Lists and replaces the variable scopes of report versions: the member of the report's dimension whose branch each scoped
/// variable reads. Rows inside the branch read their own value, rows above it read the member's value, and other rows read nothing.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/report-versions/{versionId:long}/variable-scopes")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportVariableScopesController(ReportVariableScopeService scopes) : ControllerBase
{
    /// <summary>Lists a report version's variable scopes.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The scopes.</returns>
    /// <response code="200">The scopes by variable key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportVariableScopeRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportVariableScopeRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await scopes.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces a draft or published report version's variable scopes; whether the model has each variable and the report's dimension each member is a publication rule.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The complete scopes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored scopes.</returns>
    /// <response code="200">The scopes by variable key.</response>
    /// <response code="400">A variable is scoped twice, or a scope breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<IReadOnlyList<ReportVariableScopeRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ReportVariableScopeRecord>>> Replace(
        string tenantId,
        long versionId,
        ReportVariableScopesInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await scopes.ReplaceAsync(versionId, input, cancellationToken));
    }
}
