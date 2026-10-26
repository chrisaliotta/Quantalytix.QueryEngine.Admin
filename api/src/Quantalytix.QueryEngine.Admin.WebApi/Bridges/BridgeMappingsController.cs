using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Reports;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Bridges;

/// <summary>Lists and replaces the source-member to target-member mappings of bridge versions.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/bridge-versions/{versionId:long}/mappings")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class BridgeMappingsController(BridgeMappingService mappings) : ControllerBase
{
    /// <summary>Lists every mapping of a bridge version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The mappings.</returns>
    /// <response code="200">The mappings by source member ID, then target member ID.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BridgeMappingRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BridgeMappingRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await mappings.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces every mapping of a draft or published bridge version with the given list.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published bridge version.</param>
    /// <param name="input">The complete mapping list.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored mappings.</returns>
    /// <response code="200">The stored mappings by source member ID, then target member ID.</response>
    /// <response code="400">A pair repeats, a weight is negative, or a value does not fit DECIMAL(38, 12); errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut]
    [RequireEditable(VersionKinds.BridgeVersion, "versionId")]
    [RequireAnalytical(AnalyticalTarget.BridgeVersion, "versionId")]
    [ProducesResponseType<IReadOnlyList<BridgeMappingRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<BridgeMappingRecord>>> Replace(
        string tenantId,
        long versionId,
        BridgeMappingsInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await mappings.ReplaceAsync(versionId, input, cancellationToken));
    }
}
