using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>Lists and replaces the member options of report versions: how members of the report's dimension show as rows.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/report-versions/{versionId:long}/member-options")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportMemberOptionsController(ReportMemberOptionService options) : ControllerBase
{
    /// <summary>Lists a report version's member options.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The member options.</returns>
    /// <response code="200">The options by member key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportMemberOptionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportMemberOptionRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await options.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces a draft or published report version's member options; whether the report's dimension has each member is a publication rule.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The complete member options.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored member options.</returns>
    /// <response code="200">The options by member key.</response>
    /// <response code="400">A member has two options, or an option breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<IReadOnlyList<ReportMemberOptionRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ReportMemberOptionRecord>>> Replace(
        string tenantId,
        long versionId,
        ReportMemberOptionsInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await options.ReplaceAsync(versionId, input, cancellationToken));
    }
}
