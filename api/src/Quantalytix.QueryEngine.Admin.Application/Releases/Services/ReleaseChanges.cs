using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// The release manifest change set (OI-01) behind <c>releases/{releaseId}/changes</c>. Only a draft's manifest changes (R2), which is
/// refused before any operation applies. A preview answers the release rules' report over the pending manifest and its difference
/// from the active release (OI-37).
/// </summary>
public sealed class ReleaseChanges(
    ChangeSetPipeline<ReleaseSnapshot, ReleaseOperation, ReleaseDetail> pipeline,
    ITableGateway<SemanticReleaseRecord> releases,
    ReleaseSnapshotLoader loader,
    ReleaseRules rules,
    ReleaseQueries queries)
{
    /// <summary>Applies the operations in memory, checks and diffs the result, and writes nothing.</summary>
    /// <param name="releaseId">The draft release.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pending release with its report and diff, or the refusal.</returns>
    public async Task<Outcome<ReleaseChangeSetPreview>> PreviewAsync(long releaseId, ChangeSetRequest<ReleaseOperation> request, CancellationToken cancellationToken)
    {
        if (await NotDraftAsync(releaseId, cancellationToken) is { } refusal) return Outcome<ReleaseChangeSetPreview>.Refused(refusal);
        var preview = await pipeline.PreviewAsync(releaseId, request, cancellationToken);
        if (!preview.Succeeded) return Outcome<ReleaseChangeSetPreview>.Refused(preview.Refusal);

        var state = preview.Value.State;
        var stored = await loader.LoadAsync(releaseId, cancellationToken);
        var pending = stored with
        {
            Manifest =
            [
                .. state.Manifest.Select(entry => new ReleaseDatasetRecord
                {
                    SemanticReleaseId = releaseId,
                    DatasetId = entry.DatasetId,
                    DatasetVersionId = entry.DatasetVersionId
                })
            ]
        };
        var report = await rules.CheckPendingAsync(pending, cancellationToken);
        return Outcome<ReleaseChangeSetPreview>.Success(new ReleaseChangeSetPreview
        {
            State = state,
            Violations = report.Violations,
            Report = report,
            DiffVsActive = await queries.DiffFromActiveAsync(releaseId, pending.Manifest, cancellationToken)
        });
    }

    /// <summary>Applies the operations to the draft as stored now and writes the manifest.</summary>
    /// <param name="releaseId">The draft release.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored release, or the refusal with nothing written.</returns>
    public async Task<Outcome<ChangeSetCommit<ReleaseDetail>>> CommitAsync(long releaseId, ChangeSetRequest<ReleaseOperation> request, CancellationToken cancellationToken)
    {
        if (await NotDraftAsync(releaseId, cancellationToken) is { } refusal) return Outcome<ChangeSetCommit<ReleaseDetail>>.Refused(refusal);
        return await pipeline.CommitAsync(releaseId, request, cancellationToken);
    }

    /// <summary>The conflict of a release that is not a draft; null for a draft.</summary>
    private async Task<Refusal?> NotDraftAsync(long releaseId, CancellationToken cancellationToken)
    {
        var release = await releases.GetAsync(new { SemanticReleaseId = releaseId }, cancellationToken);
        return release.Status == PublicationStatuses.Draft ? null : ReleaseRefusals.WrongStatus(release, "a draft");
    }
}
