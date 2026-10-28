using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Releases;

/// <summary>
/// Creates semantic releases, edits their manifests of pinned dataset versions, and activates them. Validating and
/// publishing a release go through the version status endpoint with kind <c>semantic_release</c>.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/releases")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class ReleasesController(ReleaseService releases, ReleaseActivator activator) : ControllerBase
{
    /// <summary>Lists the tenant's releases, newest first, with whether each is active and how many datasets it pins.</summary>
    /// <param name="tenantId">The tenant that owns the releases.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The release summaries.</returns>
    /// <response code="200">The release summaries, newest first.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReleaseSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReleaseSummary>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await releases.ListAsync(cancellationToken));
    }

    /// <summary>Creates a draft release, copying the manifest of <c>copyFromSemanticReleaseId</c> when it is given.</summary>
    /// <param name="tenantId">The tenant that owns the release.</param>
    /// <param name="input">The release key and the optional release to copy.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new draft release.</returns>
    /// <response code="201">The release was created; the body is the new draft.</response>
    /// <response code="400">The key is malformed or taken, or the release to copy does not exist.</response>
    [HttpPost]
    [ProducesResponseType<SemanticReleaseRecord>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<SemanticReleaseRecord>> Create(string tenantId, ReleaseCreateInput input, CancellationToken cancellationToken)
    {
        var release = await releases.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { tenantId, semanticReleaseId = release.SemanticReleaseId }, release);
    }

    /// <summary>Reads one release.</summary>
    /// <param name="tenantId">The tenant that owns the release.</param>
    /// <param name="semanticReleaseId">The release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The release.</returns>
    /// <response code="200">The release.</response>
    [HttpGet("{semanticReleaseId:long}")]
    [ProducesResponseType<SemanticReleaseRecord>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SemanticReleaseRecord>> Get(string tenantId, long semanticReleaseId, CancellationToken cancellationToken)
    {
        return Ok(await releases.GetAsync(semanticReleaseId, cancellationToken));
    }

    /// <summary>Deletes a draft release and its manifest.</summary>
    /// <param name="tenantId">The tenant that owns the release.</param>
    /// <param name="semanticReleaseId">The draft release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The release and its manifest are deleted.</response>
    /// <response code="409">The release is not a draft.</response>
    [HttpDelete("{semanticReleaseId:long}")]
    [RequireDraft(VersionKinds.SemanticRelease, "semanticReleaseId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string tenantId, long semanticReleaseId, CancellationToken cancellationToken)
    {
        await releases.DeleteAsync(semanticReleaseId, cancellationToken);
        return NoContent();
    }

    /// <summary>Lists a release's manifest: the dataset version it pins for each dataset.</summary>
    /// <param name="tenantId">The tenant that owns the release.</param>
    /// <param name="semanticReleaseId">The release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The manifest rows, ordered by dataset.</returns>
    /// <response code="200">The manifest rows, ordered by dataset.</response>
    [HttpGet("{semanticReleaseId:long}/datasets")]
    [ProducesResponseType<IReadOnlyList<ReleaseDatasetRecord>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReleaseDatasetRecord>>> ListDatasets(string tenantId, long semanticReleaseId, CancellationToken cancellationToken)
    {
        return Ok(await releases.ListDatasetsAsync(semanticReleaseId, cancellationToken));
    }

    /// <summary>Replaces a draft release's manifest.</summary>
    /// <param name="tenantId">The tenant that owns the release.</param>
    /// <param name="semanticReleaseId">The draft release.</param>
    /// <param name="input">The complete manifest, one row per dataset.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored manifest rows.</returns>
    /// <response code="200">The stored manifest rows, ordered by dataset.</response>
    /// <response code="400">A dataset does not exist, or a dataset or dataset version appears more than once.</response>
    /// <response code="409">The release is not a draft.</response>
    [HttpPut("{semanticReleaseId:long}/datasets")]
    [RequireDraft(VersionKinds.SemanticRelease, "semanticReleaseId")]
    [ProducesResponseType<IReadOnlyList<ReleaseDatasetRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ReleaseDatasetRecord>>> ReplaceDatasets(
        string tenantId,
        long semanticReleaseId,
        ReleaseManifestInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await releases.ReplaceDatasetsAsync(semanticReleaseId, input, cancellationToken));
    }

    /// <summary>
    /// Makes a published release the one runtime queries use: sets <c>qe_database_info.active_semantic_release_id</c> and
    /// <c>updated_at</c>, and advances the semantic revision.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the release.</param>
    /// <param name="semanticReleaseId">The published release.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The tenant's database info after the activation.</returns>
    /// <response code="200">The release is active; the body is the updated database info.</response>
    /// <response code="409">The release is not published, so nothing changed; the detail names its status.</response>
    [HttpPost("{semanticReleaseId:long}/activate")]
    [ProducesResponseType<DatabaseInfoRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DatabaseInfoRecord>> Activate(string tenantId, long semanticReleaseId, CancellationToken cancellationToken)
    {
        var result = await activator.ActivateAsync(semanticReleaseId, cancellationToken);
        if (!result.Activated)
        {
            var detail = $"{VersionKinds.SemanticRelease} {semanticReleaseId} is {result.Release.Status}; only published releases can be activated.";
            return Problems.Conflict("Release is not published", detail).ToResult();
        }

        return Ok(result.DatabaseInfo);
    }
}
