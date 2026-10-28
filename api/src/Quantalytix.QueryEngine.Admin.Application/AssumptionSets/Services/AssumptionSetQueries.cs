using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// The assumption set reads (OI-38): the list with live and changed pin counts, a set with pin states and readiness, the comparison
/// of two sets, the capture preview and the identities sets pin. The active set reads the active release.
/// </summary>
public sealed class AssumptionSetQueries(
    ITableGateway<AssumptionSetRecord> sets,
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<DatasetRecord> datasets,
    AssumptionPins pins,
    PinIdentityReader identities,
    ReleaseReportReader reports,
    ReleaseQueries releaseQueries,
    TenantInfoReader tenant)
{
    /// <summary>The identities sets pin, report-owned ones included.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Dimensions, mapping sets, then bridges, each by key.</returns>
    public async Task<IReadOnlyList<PinIdentity>> IdentitiesAsync(CancellationToken cancellationToken)
    {
        return (await identities.ReadAsync(cancellationToken)).All;
    }

    /// <summary>The active set and every historical set, newest capture first, with their pin counts.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list.</returns>
    public async Task<AssumptionSetList> ListAsync(CancellationToken cancellationToken)
    {
        var index = await identities.ReadAsync(cancellationToken);
        var pinsOf = await pins.ListAllAsync(cancellationToken);
        var items = (await sets.ListAsync(null, cancellationToken)).Select(set =>
        {
            var rows = index.Rows(pinsOf[set.AssumptionSetId]);
            return new SetListItem(set)
            {
                PinCount = rows.Count,
                LivePins = rows.Count(row => row.State == PinStates.Live),
                ChangedSinceCapture = set.Status == AssumptionSetStatuses.Historical ? rows.Count(row => row.VersionId != row.PublishedVersionId) : 0
            };
        }).ToList();
        return new AssumptionSetList
        {
            Active = items.Single(item => item.Status == AssumptionSetStatuses.Active),
            Historical = [.. items.Where(item => item.Status == AssumptionSetStatuses.Historical).OrderByDescending(item => item.CapturedAt)]
        };
    }

    /// <summary>A set with its pins and, for a historical set, whether each enabled report can be compared on it.</summary>
    /// <param name="setId">The set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The detail.</returns>
    public async Task<SetDetail> GetAsync(long setId, CancellationToken cancellationToken)
    {
        var set = await sets.GetAsync(new { AssumptionSetId = setId }, cancellationToken);
        var index = await identities.ReadAsync(cancellationToken);
        var setPins = await pins.ListAsync(setId, cancellationToken);
        var releaseId = await ReleaseOfAsync(set, cancellationToken);
        return new SetDetail
        {
            Set = new AssumptionSet(set),
            ReadsReleaseId = releaseId,
            Pins = index.Rows(setPins),
            Readiness = set.Status == AssumptionSetStatuses.Historical ? await ReadinessAsync(setPins, releaseId, set.CapturedAt!.Value, index, cancellationToken) : null
        };
    }

    /// <summary>What a capture would pin and leave out now, and the release it would record.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The preview.</returns>
    public async Task<SetCapturePreview> CapturePreviewAsync(CancellationToken cancellationToken)
    {
        var index = await identities.ReadAsync(cancellationToken);
        var active = (await pins.ListAsync((await pins.ActiveAsync(cancellationToken)).AssumptionSetId, cancellationToken))
            .Select(pin => (Kind: PinKinds.Of(pin.Kind), Owned: index.IsReportOwned(pin)))
            .ToList();
        return new SetCapturePreview
        {
            Dimensions = active.Count(pin => pin is { Kind: PinKinds.Dimension, Owned: false }),
            MappingSets = active.Count(pin => pin is { Kind: PinKinds.MappingSet, Owned: false }),
            Bridges = active.Count(pin => pin is { Kind: PinKinds.Bridge, Owned: false }),
            LeftOutDimensions = active.Count(pin => pin is { Kind: PinKinds.Dimension, Owned: true }),
            LeftOutBridges = active.Count(pin => pin is { Kind: PinKinds.Bridge, Owned: true }),
            ReleaseId = (await tenant.ReadAsync(cancellationToken)).ActiveReleaseId!.Value
        };
    }

    /// <summary>What one set pins differently from another, report-owned identities left out, and how their releases differ.</summary>
    /// <param name="fromSetId">The set compared from.</param>
    /// <param name="toSetId">The set compared to.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The comparison, or null when either names no set.</returns>
    public async Task<SetComparison?> CompareAsync(long fromSetId, long toSetId, CancellationToken cancellationToken)
    {
        var compared = (await sets.ListAsync(new { AssumptionSetId = new[] { fromSetId, toSetId } }, cancellationToken)).ToDictionary(set => set.AssumptionSetId);
        if (!compared.ContainsKey(fromSetId) || !compared.ContainsKey(toSetId)) return null;
        var index = await identities.ReadAsync(cancellationToken);
        var before = await AnalyticalPinsAsync(fromSetId, index, cancellationToken);
        var after = await AnalyticalPinsAsync(toSetId, index, cancellationToken);
        var fromRelease = await ReleaseOfAsync(compared[fromSetId], cancellationToken);
        var toRelease = await ReleaseOfAsync(compared[toSetId], cancellationToken);
        return new SetComparison
        {
            FromSetId = fromSetId,
            ToSetId = toSetId,
            Added = [.. after.Where(pin => !before.ContainsKey(pin.Key)).Select(pin => Entry(pin.Key, pin.Value))],
            Removed = [.. before.Where(pin => !after.ContainsKey(pin.Key)).Select(pin => Entry(pin.Key, pin.Value))],
            Changed =
            [
                .. after.Where(pin => before.TryGetValue(pin.Key, out var version) && version != pin.Value)
                    .Select(pin => new SetPinChange { Kind = pin.Key.Kind, IdentityId = pin.Key.IdentityId, FromVersionId = before[pin.Key], ToVersionId = pin.Value })
            ],
            Unchanged = after.Count(pin => before.TryGetValue(pin.Key, out var version) && version == pin.Value),
            Release = fromRelease == toRelease ? null : await releaseQueries.DiffOfAsync(fromRelease, toRelease, cancellationToken)
        };
    }

    /// <summary>The release a set reads: its captured one, or the active release for the active set.</summary>
    private async Task<long> ReleaseOfAsync(AssumptionSetRecord set, CancellationToken cancellationToken)
    {
        return set.SemanticReleaseId ?? (await tenant.ReadAsync(cancellationToken)).ActiveReleaseId!.Value;
    }

    /// <summary>A set's pins of analytical identities, version by identity, in list order.</summary>
    private async Task<IReadOnlyDictionary<(string Kind, long IdentityId), long>> AnalyticalPinsAsync(long setId, PinIdentities index, CancellationToken cancellationToken)
    {
        var rows = index.Rows((await pins.ListAsync(setId, cancellationToken)).Where(pin => !index.IsReportOwned(pin)));
        return rows.ToDictionary(row => (row.Kind, row.IdentityId), row => row.VersionId);
    }

    /// <summary>
    /// Per enabled report: every analytical identity it reads is pinned, and every source dataset is in the set's release. An identity
    /// created after the capture says so, because no capture of that date could pin it.
    /// </summary>
    private async Task<IReadOnlyList<SetReadiness>> ReadinessAsync(
        IReadOnlyList<PinnedVersion> setPins,
        long releaseId,
        DateTime capturedAt,
        PinIdentities index,
        CancellationToken cancellationToken)
    {
        var pinned = setPins.Select(pin => (PinKinds.Of(pin.Kind), pin.IdentityId)).ToHashSet();
        var release = await releases.GetAsync(new { SemanticReleaseId = releaseId }, cancellationToken);
        var releaseManifest = await manifest.ListAsync(new { SemanticReleaseId = releaseId }, cancellationToken);
        var inRelease = releaseManifest.Select(row => row.DatasetId).ToHashSet();
        var datasetKeys = (await datasets.ListAsync(null, cancellationToken)).ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        return
        [
            .. (await reports.ListAsync(releaseManifest, cancellationToken)).Where(report => report.IsEnabled).Select(report =>
            {
                SetReadinessReason[] reasons =
                [
                    .. report.Reads.Where(read => !pinned.Contains((read.Kind, read.IdentityId))).Select(read =>
                    {
                        var identity = index.Of(read.Kind, read.IdentityId);
                        var after = identity.CreatedAt > capturedAt ? " (created after the capture)" : "";
                        return new SetReadinessReason
                        {
                            Message = $"Does not pin {read.Kind.Replace('_', ' ')} '{identity.Key}'{after}.",
                            Target = ReleaseFindings.Target(read.Kind, read.IdentityId)
                        };
                    }),
                    .. report.Sources.Where(source => !inRelease.Contains(source.DatasetId)).Select(source => new SetReadinessReason
                    {
                        Message = $"Release '{release.ReleaseKey}' does not pin dataset '{datasetKeys[source.DatasetId]}', which source '{source.Key}' reads.",
                        Target = ReleaseFindings.Target(ReleaseTargetAreas.Report, report.Id, source.Key)
                    })
                ];
                return new SetReadiness { ReportId = report.Id, Ready = reasons.Length == 0, Reasons = reasons };
            })
        ];
    }

    /// <summary>A pin entry of a comparison.</summary>
    private static SetPinEntry Entry((string Kind, long IdentityId) identity, long versionId)
    {
        return new SetPinEntry { Kind = identity.Kind, IdentityId = identity.IdentityId, VersionId = versionId };
    }
}
