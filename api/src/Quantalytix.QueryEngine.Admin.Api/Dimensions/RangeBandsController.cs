using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Dimensions;

/// <summary>Reads and replaces the range bands of versions of range dimensions.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/dimension-versions/{dimensionVersionId:long}/range-bands")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class RangeBandsController(RangeBandService bands) : ControllerBase
{
    /// <summary>Lists the bands of a version of a range dimension.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="version">A version of a range dimension.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The bands by ordinal.</returns>
    /// <response code="200">The bands by ordinal.</response>
    /// <response code="400">The version's dimension is not a range dimension; the error is keyed <c>dimensionVersionId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RangeBandRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<RangeBandRecord>>> List(
        string tenantId,
        [FromRoute] RangeVersionReference version,
        CancellationToken cancellationToken)
    {
        return Ok(await bands.ListAsync(version, cancellationToken));
    }

    /// <summary>
    /// Replaces every band of a draft or published range version with the ordered list; each band's ordinal is its position, from 1. Bound
    /// families, order and overlaps are checked when the version is validated.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="version">A draft or published version of a range dimension.</param>
    /// <param name="input">The complete band list, in order.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored bands.</returns>
    /// <response code="200">The stored bands by ordinal.</response>
    /// <response code="400">The dimension is not a range dimension, or a band breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut]
    [RequireEditable(VersionKinds.DimensionVersion, "dimensionVersionId")]
    [ProducesResponseType<IReadOnlyList<RangeBandRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<RangeBandRecord>>> Replace(
        string tenantId,
        [FromRoute] RangeVersionReference version,
        RangeBandsInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await bands.ReplaceAsync(version, input, cancellationToken));
    }
}
