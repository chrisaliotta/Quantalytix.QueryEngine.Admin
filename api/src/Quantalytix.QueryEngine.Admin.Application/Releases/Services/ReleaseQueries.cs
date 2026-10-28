using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The release reads: the list with activity and differences from the active release, a release, a diff and a check.</summary>
public sealed class ReleaseQueries(
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<ReleaseActivationRecord> activations,
    TenantInfoReader tenant,
    ReleaseSnapshotLoader loader,
    ReleaseReportReader reports,
    ReleaseRules rules)
{
    /// <summary>Every release, newest first, each but the active one with what activating it would change.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items.</returns>
    public async Task<IReadOnlyList<ReleaseListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var pins = (await manifest.ListAsync(null, cancellationToken)).ToLookup(row => row.SemanticReleaseId);
        var activeId = (await tenant.ReadAsync(cancellationToken)).ActiveReleaseId;
        var lastActivation = activeId is { } activated
            ? (await activations.ListAsync(new { SemanticReleaseId = activated }, cancellationToken)).MaxBy(row => row.ActivatedAt)
            : null;
        var readBy = await reports.ReadByAsync(cancellationToken);
        return
        [
            .. (await releases.ListAsync(null, cancellationToken))
                .OrderByDescending(row => row.CreatedAt)
                .ThenBy(row => row.ReleaseKey, StringComparer.Ordinal)
                .Select(row =>
                {
                    var isActive = row.SemanticReleaseId == activeId;
                    return new ReleaseListItem(row)
                    {
                        IsActive = isActive,
                        DatasetCount = pins[row.SemanticReleaseId].Count(),
                        DiffVsActive = activeId is { } active && !isActive
                            ? ReleaseReadModels.Diff(active, pins[active], row.SemanticReleaseId, pins[row.SemanticReleaseId], readBy)
                            : null,
                        ActivatedAt = isActive ? lastActivation?.ActivatedAt : null,
                        ActivatedBy = isActive ? lastActivation?.ActivatedBy : null
                    };
                })
        ];
    }

    /// <summary>A release with its manifest and activation history.</summary>
    /// <param name="releaseId">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The detail.</returns>
    public async Task<ReleaseDetail> GetAsync(long releaseId, CancellationToken cancellationToken)
    {
        return ReleaseReadModels.Detail(await loader.LoadAsync(releaseId, cancellationToken));
    }

    /// <summary>What changes going from one release's manifest to another's.</summary>
    /// <param name="fromReleaseId">The release compared from.</param>
    /// <param name="toReleaseId">The release compared to.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The difference, or null when either names no release.</returns>
    public async Task<ReleaseDiff?> DiffAsync(long fromReleaseId, long toReleaseId, CancellationToken cancellationToken)
    {
        long[] ids = [.. new[] { fromReleaseId, toReleaseId }.Distinct()];
        if ((await releases.ListAsync(new { SemanticReleaseId = ids }, cancellationToken)).Count != ids.Length) return null;
        return await DiffOfAsync(fromReleaseId, toReleaseId, cancellationToken);
    }

    /// <summary>What changes going from one stored release's manifest to another's.</summary>
    /// <param name="fromReleaseId">The release compared from.</param>
    /// <param name="toReleaseId">The release compared to.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The difference.</returns>
    public async Task<ReleaseDiff> DiffOfAsync(long fromReleaseId, long toReleaseId, CancellationToken cancellationToken)
    {
        var pins = (await manifest.ListAsync(new { SemanticReleaseId = new[] { fromReleaseId, toReleaseId } }, cancellationToken)).ToLookup(row => row.SemanticReleaseId);
        return ReleaseReadModels.Diff(fromReleaseId, pins[fromReleaseId], toReleaseId, pins[toReleaseId], await reports.ReadByAsync(cancellationToken));
    }

    /// <summary>A manifest, stored or pending, compared with the active release's.</summary>
    /// <param name="releaseId">The release the manifest belongs to.</param>
    /// <param name="rows">Its manifest.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The difference from the active release, or null when no release is active.</returns>
    public async Task<ReleaseDiff?> DiffFromActiveAsync(long releaseId, IReadOnlyList<ReleaseDatasetRecord> rows, CancellationToken cancellationToken)
    {
        if ((await tenant.ReadAsync(cancellationToken)).ActiveReleaseId is not { } activeId) return null;
        var active = await manifest.ListAsync(new { SemanticReleaseId = activeId }, cancellationToken);
        return ReleaseReadModels.Diff(activeId, active, releaseId, rows, await reports.ReadByAsync(cancellationToken));
    }

    /// <summary>What every report reads, its sources' datasets resolved in the active release.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report references, by key.</returns>
    public async Task<IReadOnlyList<ReleaseReportRef>> ReportReadsAsync(CancellationToken cancellationToken)
    {
        var activeId = (await tenant.ReadAsync(cancellationToken)).ActiveReleaseId;
        return await reports.ListAsync(await manifest.ListAsync(new { SemanticReleaseId = activeId!.Value }, cancellationToken), cancellationToken);
    }

    /// <summary>Runs every release rule over the saved manifest and changes nothing.</summary>
    /// <param name="releaseId">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report; <c>unsaved</c> is false.</returns>
    public async Task<ReleaseCheckReport> CheckAsync(long releaseId, CancellationToken cancellationToken)
    {
        return await rules.CheckAsync(await loader.LoadAsync(releaseId, cancellationToken), cancellationToken);
    }
}
