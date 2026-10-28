using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Reports;
using Quantalytix.QueryEngine.Admin.WebApi.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Bridges;

/// <summary>Lists, creates, clones, edits and deletes bridge versions. Status changes go through the versions status endpoint.</summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class BridgeVersionsController(BridgeVersionService versions) : ControllerBase
{
    /// <summary>Lists a bridge's versions with their allocation semantics and status.</summary>
    /// <param name="tenantId">The tenant that owns the bridge.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The versions by version number.</returns>
    /// <response code="200">The versions by version number.</response>
    [HttpGet("bridges/{bridgeId:long}/versions")]
    [ProducesResponseType<IReadOnlyList<BridgeVersionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BridgeVersionRecord>>> List(string tenantId, long bridgeId, CancellationToken cancellationToken)
    {
        return Ok(await versions.ListAsync(bridgeId, cancellationToken));
    }

    /// <summary>
    /// Creates a draft with the bridge's next version number: empty, or with copies of the mappings of <c>cloneFromVersionId</c>.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the bridge.</param>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="input">The cardinality, allocation mode and optional clone source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created draft.</returns>
    /// <response code="200">The created draft.</response>
    /// <response code="400">The input breaks a field rule, such as an unknown allocation mode; errors are keyed by camelCase JSON path.</response>
    [HttpPost("bridges/{bridgeId:long}/versions")]
    [RequireAnalytical(AnalyticalTarget.Bridge, "bridgeId")]
    [ProducesResponseType<BridgeVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BridgeVersionRecord>> Create(
        string tenantId,
        long bridgeId,
        NewBridgeVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.CreateAsync(bridgeId, input, cancellationToken));
    }

    /// <summary>Reads one bridge version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version.</returns>
    /// <response code="200">The version.</response>
    [HttpGet("bridge-versions/{versionId:long}")]
    [ProducesResponseType<BridgeVersionRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BridgeVersionRecord>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await versions.GetAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces a draft or published version's cardinality and allocation mode; its mappings stay as they are.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft or published bridge version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored version.</returns>
    /// <response code="200">The stored version.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("bridge-versions/{versionId:long}")]
    [RequireEditable(VersionKinds.BridgeVersion, "versionId")]
    [RequireAnalytical(AnalyticalTarget.BridgeVersion, "versionId")]
    [ProducesResponseType<BridgeVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BridgeVersionRecord>> Update(
        string tenantId,
        long versionId,
        BridgeVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.UpdateAsync(versionId, input, cancellationToken));
    }

    /// <summary>Deletes a draft version with its mappings.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft bridge version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the version is not a draft.</returns>
    /// <response code="204">The version and its mappings are deleted.</response>
    /// <response code="409">Nothing was deleted: the version is not a draft.</response>
    [HttpDelete("bridge-versions/{versionId:long}")]
    [RequireDraft(VersionKinds.BridgeVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        await versions.DeleteAsync(versionId, cancellationToken);
        return NoContent();
    }
}
