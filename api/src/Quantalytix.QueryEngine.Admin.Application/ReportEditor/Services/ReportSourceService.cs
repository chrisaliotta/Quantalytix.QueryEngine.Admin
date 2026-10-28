using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Creates, edits and deletes report sources. A bridge source owns one bridge, from its role's dimension into the report dimension,
/// whose one version is published at once, so the source's rules are live edits like the rest of the report. The bridge is
/// many-to-many and replicates: each rule adds an item, or every item under a member, into one row with its factor, and an item
/// may add into several rows. A bridge source may start as a copy of a sibling's rules, which is never linked. A direct source
/// stores its field. Dataset, mapping method, role and field are fixed once created.
/// </summary>
public sealed class ReportSourceService(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<DimensionRoleRecord> roles,
    ReportBridges bridges,
    ReportContentRemover remover,
    ReportChanges changes,
    IIdGenerator ids)
{
    /// <summary>Creates a source, and a bridge source's bridge.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The new source, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The violations that kept an enabled report from taking the source, in which case nothing was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> CreateAsync(long reportId, NewReportSourceInput input, CancellationToken cancellationToken)
    {
        var source = new ReportSourceRecord
        {
            ReportSourceId = ids.NextId(),
            ReportId = reportId,
            SourceKey = input.SourceKey,
            DisplayName = input.DisplayName,
            DatasetId = input.DatasetId,
            MappingMethod = input.MappingMethod,
            FieldKey = input.FieldKey,
            DimensionRoleId = input.DimensionRoleId,
            TimeFieldKey = input.TimeFieldKey
        };
        return await changes.ApplyAsync(reportId, report => report with { Sources = [.. report.Sources, source] }, async () =>
        {
            var stored = source.DimensionRoleId is { } dimensionRoleId
                ? source with { BridgeId = await CreateBridgeAsync(source, dimensionRoleId, input.CopyFromSourceId, cancellationToken) }
                : source;
            await sources.InsertAsync(stored, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>Edits a source's label, which a bridge source's bridge takes too, and its time field.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="sourceId">The source, one of the report's.</param>
    /// <param name="input">The source's new attributes, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The violations that kept an enabled report from taking the edit, in which case nothing was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> UpdateAsync(long reportId, long sourceId, ReportSourceInput input, CancellationToken cancellationToken)
    {
        var source = await sources.GetAsync(new { ReportSourceId = sourceId }, cancellationToken);
        var updated = source with { DisplayName = input.DisplayName, TimeFieldKey = input.TimeFieldKey };
        return await changes.ApplyAsync(
            reportId,
            report => report with { Sources = [.. report.Sources.Where(other => other.ReportSourceId != sourceId), updated] },
            async () =>
            {
                await sources.UpdateAsync(updated, cancellationToken);
                if (updated.BridgeId is { } bridgeId && updated.DisplayName != source.DisplayName)
                    await bridges.RenameAsync(bridgeId, updated.DisplayName, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>Deletes a source with its variables, the profile picks that name it and a bridge source's bridge.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="sourceId">The source, one of the report's.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>The violations that kept an enabled report from losing the source, in which case nothing was deleted.</returns>
    public async Task<IReadOnlyList<RuleViolation>> DeleteAsync(long reportId, long sourceId, CancellationToken cancellationToken)
    {
        var source = await sources.GetAsync(new { ReportSourceId = sourceId }, cancellationToken);
        return await changes.ApplyAsync(
            reportId,
            report => report with
            {
                Sources = [.. report.Sources.Where(other => other.ReportSourceId != sourceId)],
                SourceVariables = [.. report.SourceVariables.Where(variable => variable.ReportSourceId != sourceId)],
                SourceItems = [.. report.SourceItems.Where(item => item.ReportSourceId != sourceId)],
                Picks = [.. report.Picks.Where(pick => pick.ReportSourceId != sourceId)]
            },
            () => remover.RemoveSourcesAsync([source], cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Creates a bridge source's bridge from its role's dimension into the report dimension, with a live version that replicates each
    /// item into every row its rules name, starting from a copy of a sibling's rules.
    /// </summary>
    private async Task<long> CreateBridgeAsync(ReportSourceRecord source, long dimensionRoleId, long? copyFromSourceId, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { source.ReportId }, cancellationToken);
        var role = await roles.GetAsync(new { DimensionRoleId = dimensionRoleId }, cancellationToken);
        long? template = copyFromSourceId is { } siblingId ? (await sources.GetAsync(new { ReportSourceId = siblingId }, cancellationToken)).BridgeId : null;
        return await bridges.CreateAsync(
            new ReportBridgeInput
            {
                Key = ReportSourceKeys.Bridge(report.ReportKey, source.SourceKey),
                DisplayName = source.DisplayName,
                SourceDimensionId = role.DimensionId,
                ReportDimensionId = report.ReportDimensionId,
                CopyFromBridgeId = template
            },
            cancellationToken);
    }
}
