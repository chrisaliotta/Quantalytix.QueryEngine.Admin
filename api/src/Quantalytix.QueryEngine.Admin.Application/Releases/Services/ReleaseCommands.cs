using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// The release lifecycle commands, which run on stored state (R2–R7): create a draft, delete it, the strict transitions validate,
/// publish, reopen and retire, and activation. Validate and publish run every release rule; publish stamps the content hash (OI-41);
/// retire runs <see cref="ReleaseNotActiveRule"/>; activation re-checks every enabled report, points the tenant at the release, advances
/// the semantic revision and logs the activation (OI-40).
/// </summary>
public sealed class ReleaseCommands(
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<ReleaseActivationRecord> activations,
    ITableGateway<DatabaseInfoRecord> databaseInfo,
    ReleaseSnapshotLoader loader,
    ReleaseRules rules,
    ReleaseReportChecks reports,
    ReleaseNotActiveRule notActive,
    TenantInfoReader tenant,
    SemanticRevision semanticRevision,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>Creates a draft, copying another release's manifest when asked. The release row is written first, so a stop leaves an empty draft.</summary>
    /// <param name="input">The validated key and source release.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The draft, or a conflict when the key is taken.</returns>
    public async Task<Outcome<Release>> CreateAsync(CreateReleaseInput input, CancellationToken cancellationToken)
    {
        if ((await releases.ListAsync(new { ReleaseKey = input.Key }, cancellationToken)).Count > 0)
            return Outcome<Release>.Refused(Refusal.Conflict("Key taken", $"Another release already uses the key '{input.Key}'."));

        var release = new SemanticReleaseRecord
        {
            SemanticReleaseId = ids.NextId(),
            ReleaseKey = input.Key,
            Status = PublicationStatuses.Draft,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await releases.InsertAsync(release, cancellationToken);
        if (input.CopyFromReleaseId is { } sourceId)
        {
            var rows = await manifest.ListAsync(new { SemanticReleaseId = sourceId }, cancellationToken);
            await manifest.InsertManyAsync([.. rows.Select(row => row with { SemanticReleaseId = release.SemanticReleaseId })], cancellationToken);
        }

        return Outcome<Release>.Success(new Release(release));
    }

    /// <summary>Deletes a draft (R6): its manifest rows first, then the release.</summary>
    /// <param name="releaseId">The release.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when deleted; a conflict when it is not a draft.</returns>
    public async Task<Refusal?> DeleteAsync(long releaseId, CancellationToken cancellationToken)
    {
        var release = await GetAsync(releaseId, cancellationToken);
        if (release.Status != PublicationStatuses.Draft) return ReleaseRefusals.WrongStatus(release, "a draft");
        await manifest.DeleteAsync(new { SemanticReleaseId = releaseId }, cancellationToken);
        await releases.DeleteAsync(new { SemanticReleaseId = releaseId }, cancellationToken);
        return null;
    }

    /// <summary>Moves a draft to validated when every release rule passes.</summary>
    /// <param name="releaseId">The draft.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The release and its clean report, a conflict when it is not a draft, or the broken rules.</returns>
    public async Task<Outcome<ReleaseTransitionResult>> ValidateAsync(long releaseId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadAsync(releaseId, cancellationToken);
        if (snapshot.Release.Status != PublicationStatuses.Draft)
            return Outcome<ReleaseTransitionResult>.Refused(ReleaseRefusals.WrongStatus(snapshot.Release, "a draft"));
        return await PassAsync(snapshot, snapshot.Release with { Status = PublicationStatuses.Validated }, cancellationToken);
    }

    /// <summary>Publishes a validated release when every release rule still passes, with its content hash (R13). Activates nothing (R3).</summary>
    /// <param name="releaseId">The validated release.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The release and its clean report, a conflict when it is not validated, or the broken rules.</returns>
    public async Task<Outcome<ReleaseTransitionResult>> PublishAsync(long releaseId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadAsync(releaseId, cancellationToken);
        if (snapshot.Release.Status != PublicationStatuses.Validated)
            return Outcome<ReleaseTransitionResult>.Refused(ReleaseRefusals.WrongStatus(snapshot.Release, "validated"));
        var published = snapshot.Release with
        {
            Status = PublicationStatuses.Published,
            PublishedAt = time.GetUtcNow().UtcDateTime,
            ContentHash = ReleaseContentHash.Of(snapshot.Manifest)
        };
        return await PassAsync(snapshot, published, cancellationToken);
    }

    /// <summary>Returns a validated release to draft.</summary>
    /// <param name="releaseId">The validated release.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The draft, or a conflict when it is not validated.</returns>
    public async Task<Outcome<Release>> ReopenAsync(long releaseId, CancellationToken cancellationToken)
    {
        var release = await GetAsync(releaseId, cancellationToken);
        if (release.Status != PublicationStatuses.Validated) return Outcome<Release>.Refused(ReleaseRefusals.WrongStatus(release, "validated"));
        return Outcome<Release>.Success(await SaveAsync(release with { Status = PublicationStatuses.Draft }, cancellationToken));
    }

    /// <summary>Retires a published release that is not the active one (R5).</summary>
    /// <param name="releaseId">The published release.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The retired release, a conflict when it is not published, or <c>release_not_active</c>.</returns>
    public async Task<Outcome<Release>> RetireAsync(long releaseId, CancellationToken cancellationToken)
    {
        var release = await GetAsync(releaseId, cancellationToken);
        if (release.Status != PublicationStatuses.Published) return Outcome<Release>.Refused(ReleaseRefusals.WrongStatus(release, "published"));
        var violations = await notActive.EvaluateAsync(releaseId, cancellationToken);
        if (violations.Count > 0) return Outcome<Release>.Refused(Refusal.RulesViolated(violations[0].Message, violations));
        return Outcome<Release>.Success(await SaveAsync(release with { Status = PublicationStatuses.Retired }, cancellationToken));
    }

    /// <summary>
    /// Makes a published release the active one (R4); activating an older release is a rollback. Every enabled report is checked
    /// against the release first (R12). The pointer is written before the revision advances, so the engine never caches the previous
    /// release under the new revision; the log row is written last, so a stop never logs an activation that did not happen.
    /// </summary>
    /// <param name="releaseId">The published release.</param>
    /// <param name="activatedBy">The caller's token subject.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The tenant state after the activation, a conflict when the release is not published, or the reports' broken rules.</returns>
    public async Task<Outcome<TenantInfo>> ActivateAsync(long releaseId, string activatedBy, CancellationToken cancellationToken)
    {
        var release = await GetAsync(releaseId, cancellationToken);
        if (release.Status != PublicationStatuses.Published) return Outcome<TenantInfo>.Refused(ReleaseRefusals.WrongStatus(release, "published"));
        var violations = await reports.AllAsync(releaseId, cancellationToken);
        if (violations.Count > 0)
        {
            var detail = $"Enabled reports break rules on release '{release.ReleaseKey}'; the active release is unchanged.";
            return Outcome<TenantInfo>.Refused(Refusal.RulesViolated(detail, [.. violations.Select(violation => violation.ToRuleViolation())]));
        }

        var now = time.GetUtcNow().UtcDateTime;
        var info = await databaseInfo.GetAsync(new { SingletonId }, cancellationToken);
        await databaseInfo.UpdateAsync(info with { ActiveSemanticReleaseId = releaseId, UpdatedAt = now }, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        await activations.InsertAsync(
            new ReleaseActivationRecord
            {
                ReleaseActivationId = ids.NextId(),
                SemanticReleaseId = releaseId,
                PreviousSemanticReleaseId = info.ActiveSemanticReleaseId,
                ActivatedAt = now,
                ActivatedBy = activatedBy
            },
            cancellationToken);
        return Outcome<TenantInfo>.Success(await tenant.ReadAsync(cancellationToken));
    }

    /// <summary>Writes a transition once every release rule passes on the stored manifest.</summary>
    private async Task<Outcome<ReleaseTransitionResult>> PassAsync(ReleaseSnapshot snapshot, SemanticReleaseRecord moved, CancellationToken cancellationToken)
    {
        var report = await rules.CheckAsync(snapshot, cancellationToken);
        if (report.Violations.Count > 0)
        {
            var detail = $"Release '{snapshot.Release.ReleaseKey}' breaks rules; its status is unchanged.";
            return Outcome<ReleaseTransitionResult>.Refused(Refusal.RulesViolated(detail, [.. report.Violations.Select(violation => violation.ToRuleViolation())]));
        }

        return Outcome<ReleaseTransitionResult>.Success(new ReleaseTransitionResult { Release = await SaveAsync(moved, cancellationToken), Report = report });
    }

    /// <summary>Reads a release row.</summary>
    private Task<SemanticReleaseRecord> GetAsync(long releaseId, CancellationToken cancellationToken)
    {
        return releases.GetAsync(new { SemanticReleaseId = releaseId }, cancellationToken);
    }

    /// <summary>Writes a release row and answers it.</summary>
    private async Task<Release> SaveAsync(SemanticReleaseRecord release, CancellationToken cancellationToken)
    {
        await releases.UpdateAsync(release, cancellationToken);
        return new Release(release);
    }
}
