using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.WebApi.Mappings;

/// <summary>Lists, creates, clones and deletes mapping versions. Status changes go through the versions status endpoint.</summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class MappingVersionsController(MappingVersionService versions) : ControllerBase
{
    /// <summary>Lists a mapping set's versions with their status.</summary>
    /// <param name="tenantId">The tenant that owns the mapping set.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The versions by version number.</returns>
    /// <response code="200">The versions by version number.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("mapping-sets/{mappingSetId:long}/versions")]
    [ProducesResponseType<IReadOnlyList<MappingVersionRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MappingVersionRecord>>> List(
        string tenantId,
        long mappingSetId,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.ListAsync(mappingSetId, cancellationToken));
    }

    /// <summary>
    /// Creates a draft with the set's next version number: empty, or with copies of the entries of <c>cloneFromVersionId</c>.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the mapping set.</param>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="input">The optional clone source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created draft.</returns>
    /// <response code="200">The created draft.</response>
    /// <response code="400">The clone source is not a version of the mapping set; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost("mapping-sets/{mappingSetId:long}/versions")]
    [ProducesResponseType<MappingVersionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<MappingVersionRecord>> Create(
        string tenantId,
        long mappingSetId,
        NewMappingVersionInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await versions.CreateAsync(mappingSetId, input, cancellationToken));
    }

    /// <summary>Reads one mapping version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version.</returns>
    /// <response code="200">The version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("mapping-versions/{versionId:long}")]
    [ProducesResponseType<MappingVersionRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MappingVersionRecord>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await versions.GetAsync(versionId, cancellationToken));
    }

    /// <summary>Deletes a draft mapping version: its entries, then the version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft mapping version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The version and its entries are deleted.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpDelete("mapping-versions/{versionId:long}")]
    [RequireDraft(VersionKinds.MappingVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        await versions.DeleteAsync([versionId], cancellationToken);
        return NoContent();
    }
}
