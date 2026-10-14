using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// Points the tenant's runtime queries at a published release through <c>qe_database_info.active_release_id</c>, then
/// advances the semantic revision so the query engine reloads its metadata. Activation is how dataset versions and
/// releases reach queries; publishing or retiring a release changes neither.
/// </summary>
public sealed class ReleaseActivator(
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<DatabaseInfoRecord> databaseInfo,
    SemanticRevision semanticRevision,
    TimeProvider time)
{
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>
    /// Activates a release. The release's status is checked before anything is written: a release that is not published
    /// is refused and the database info stays as it is.
    /// </summary>
    /// <param name="releaseId">The release to activate.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Whether the release was activated, the release, and the database info after the request.</returns>
    public async Task<ReleaseActivationResult> ActivateAsync(long releaseId, CancellationToken cancellationToken)
    {
        var release = await releases.GetAsync(new { ReleaseId = releaseId }, cancellationToken);
        var info = await databaseInfo.GetAsync(new { SingletonId }, cancellationToken);
        if (release.Status != PublicationStatuses.Published)
            return new ReleaseActivationResult { Activated = false, Release = release, DatabaseInfo = info };

        await databaseInfo.UpdateAsync(info with { ActiveReleaseId = releaseId, UpdatedAt = time.GetUtcNow().UtcDateTime }, cancellationToken);
        // The pointer is written before the revision advances, so the engine never caches the previous release under the new revision.
        await semanticRevision.AdvanceAsync(cancellationToken);
        var activated = await databaseInfo.GetAsync(new { SingletonId }, cancellationToken);
        return new ReleaseActivationResult { Activated = true, Release = release, DatabaseInfo = activated };
    }
}
