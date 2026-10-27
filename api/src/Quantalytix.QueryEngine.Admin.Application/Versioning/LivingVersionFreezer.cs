using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// The freeze of a living kind (dimension, mapping and bridge versions, OI-14): the live version n must pass every rule of its
/// kind; then it is copied to n+1 and n+1 is published through <see cref="VersionWorkflow.PublishAsync"/>, whose handlers retire
/// n (<see cref="PredecessorRetirer"/>), repoint the active assumption set (<c>ActiveSetPinner</c>), rebuild derived rows (the
/// dimension closure) and advance the semantic revision. A version that breaks a rule is refused and nothing changes.
/// </summary>
/// <param name="stores">Reads the version's status.</param>
/// <param name="workflow">Runs the rules and publishes the copy.</param>
/// <param name="copiers">One copier per living kind.</param>
/// <param name="time">Stamps the report.</param>
public sealed class LivingVersionFreezer(
    VersionStoreRegistry stores,
    VersionWorkflow workflow,
    IEnumerable<ILivingVersionCopier> copiers,
    TimeProvider time)
{
    /// <summary>The title of the 409 refusal of a version that is not live.</summary>
    public const string NotLiveTitle = "Version is not live";

    /// <summary>Freezes the live version of a living kind.</summary>
    /// <param name="kind">A living <c>version_kind</c> value.</param>
    /// <param name="versionId">The version to freeze, which must be the identity's live (published) version.</param>
    /// <param name="note">Why the version is frozen, such as <c>FY26 close</c>, or null.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The report and the new live version, or a 409 refusal when the version is not live, or a 422 refusal with the violations.</returns>
    public async Task<Outcome<FrozenVersion>> FreezeAsync(string kind, long versionId, string? note, CancellationToken cancellationToken)
    {
        var version = await stores.Get(kind).GetAsync(versionId, cancellationToken);
        if (version.Status != PublicationStatuses.Published)
            return Outcome<FrozenVersion>.Refused(Refusal.Conflict(NotLiveTitle, $"{kind} {versionId} is {version.Status}; only the live version can be frozen."));

        var checkedAt = time.GetUtcNow().UtcDateTime;
        var violations = await workflow.CheckAsync(kind, versionId, cancellationToken);
        if (violations.Count > 0)
            return Outcome<FrozenVersion>.Refused(Refusal.RulesViolated($"{kind} {versionId} breaks its rules; nothing was frozen.", violations));

        var liveVersionId = await copiers.Single(copier => copier.Kind == kind).CopyAsync(versionId, cancellationToken);
        await workflow.PublishAsync(kind, liveVersionId, cancellationToken);
        // TODO(G2, SG-04/SG-05): store note as version n's freeze_note, and its retired_at, once qe_<kind> has the columns.
        return Outcome<FrozenVersion>.Success(new FrozenVersion
        {
            Report = new ValidationReport { VersionId = versionId, CheckedAt = checkedAt, Violations = [] },
            LiveVersionId = liveVersionId
        });
    }
}
