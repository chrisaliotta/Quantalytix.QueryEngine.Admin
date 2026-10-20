using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists and replaces the variable targets of report versions: the member of the report's dimension each targeted variable reads.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/report-versions/{versionId:long}/variable-targets")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportVariableTargetsController(ReportVariableTargetService targets) : ControllerBase
{
    /// <summary>Lists a report version's variable targets.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The targets.</returns>
    /// <response code="200">The targets by variable key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportVariableTargetRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportVariableTargetRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await targets.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces a draft or published report version's variable targets; whether the model has each variable and the report's dimension each member is a publication rule.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The complete targets.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored targets.</returns>
    /// <response code="200">The targets by variable key.</response>
    /// <response code="400">A variable is targeted twice, or a target breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<IReadOnlyList<ReportVariableTargetRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ReportVariableTargetRecord>>> Replace(
        string tenantId,
        long versionId,
        ReportVariableTargetsInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await targets.ReplaceAsync(versionId, input, cancellationToken));
    }
}
