using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Mappings;

/// <summary>Lists and edits the entries of mapping versions, one at a time or as a complete list.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/mapping-versions/{versionId:long}/entries")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class MappingEntriesController(MappingEntryService entries) : ControllerBase
{
    /// <summary>Lists every entry of a mapping version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The entries by source key.</returns>
    /// <response code="200">The entries by source key, compared exactly.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DimensionMappingRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DimensionMappingRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await entries.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Replaces every entry of a draft mapping version with the given list.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft mapping version.</param>
    /// <param name="input">The complete entry list.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored entries.</returns>
    /// <response code="200">The stored entries by source key, under new entry IDs.</response>
    /// <response code="400">A source key is empty, too long or repeated; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut]
    [RequireDraft(VersionKinds.MappingVersion, "versionId")]
    [ProducesResponseType<IReadOnlyList<DimensionMappingRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<DimensionMappingRecord>>> Replace(
        string tenantId,
        long versionId,
        MappingEntriesInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await entries.ReplaceAsync(versionId, input, cancellationToken));
    }

    /// <summary>Adds an entry to a draft mapping version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft mapping version.</param>
    /// <param name="input">The new entry.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The created entry.</returns>
    /// <response code="200">The created entry.</response>
    /// <response code="400">The input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPost]
    [RequireDraft(VersionKinds.MappingVersion, "versionId")]
    [ProducesResponseType<DimensionMappingRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionMappingRecord>> Create(
        string tenantId,
        long versionId,
        MappingEntryInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await entries.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>Replaces an entry's source value and member.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="entry">The draft mapping version and its entry.</param>
    /// <param name="input">The entry's new values.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored entry.</returns>
    /// <response code="200">The stored entry.</response>
    /// <response code="400">The entry is not in the version, or the input breaks a field rule; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("{entryId:long}")]
    [RequireDraft(VersionKinds.MappingVersion, "versionId")]
    [ProducesResponseType<DimensionMappingRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DimensionMappingRecord>> Update(
        string tenantId,
        [FromRoute] MappingEntryReference entry,
        MappingEntryInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await entries.UpdateAsync(entry, input, cancellationToken));
    }

    /// <summary>Deletes an entry of a draft mapping version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="entry">The draft mapping version and its entry.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The entry is deleted.</response>
    /// <response code="400">The entry is not in the version; the error is keyed <c>entryId</c>.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpDelete("{entryId:long}")]
    [RequireDraft(VersionKinds.MappingVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] MappingEntryReference entry, CancellationToken cancellationToken)
    {
        await entries.DeleteAsync(entry, cancellationToken);
        return NoContent();
    }
}
