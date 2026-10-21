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
    BridgeService bridges,
    BridgeVersionService bridgeVersions,
    BridgeMappingService bridgeMappings,
    OwnedVersions owned,
    VersionWorkflow workflow,
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
            RoleId = input.RoleId,
            TimeFieldKey = input.TimeFieldKey
        };
        return await changes.ApplyAsync(reportId, report => report with { Sources = [.. report.Sources, source] }, async () =>
        {
            var stored = source.RoleId is { } roleId
                ? source with { BridgeId = await CreateBridgeAsync(source, roleId, input.CopyFromSourceId, cancellationToken) }
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
                    await bridges.UpdateAsync(bridgeId, new BridgeInput { DisplayName = updated.DisplayName, IsEnabled = true }, cancellationToken);
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
    /// Creates a bridge source's bridge from its role's dimension into the report dimension, with a version that replicates each
    /// item into every row its rules name, starting from a copy of a sibling's rules, and publishes the version.
    /// </summary>
    private async Task<long> CreateBridgeAsync(ReportSourceRecord source, long roleId, long? copyFromSourceId, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { source.ReportId }, cancellationToken);
        var role = await roles.GetAsync(new { RoleId = roleId }, cancellationToken);
        var bridge = await bridges.CreateAsync(
            new NewBridgeInput
            {
                BridgeKey = ReportSourceKeys.Bridge(report.ReportKey, source.SourceKey),
                DisplayName = source.DisplayName,
                SourceDimensionId = role.DimensionId,
                TargetDimensionId = report.ReportDimensionId,
                IsEnabled = true
            },
            cancellationToken);
        var version = await bridgeVersions.CreateAsync(
            bridge.BridgeId,
            new NewBridgeVersionInput { Cardinality = Cardinalities.ManyToMany, AllocationMode = AllocationModes.Replicate },
            cancellationToken);
        if (copyFromSourceId is { } siblingId)
        {
            var sibling = await sources.GetAsync(new { ReportSourceId = siblingId }, cancellationToken);
            var template = (await owned.BridgesAsync([sibling], cancellationToken))[siblingId];
            var copied = (await bridgeMappings.ListAsync(template.BridgeVersionId, cancellationToken)).Select(BridgeEntries.Kept);
            await bridgeMappings.ReplaceAsync(version.BridgeVersionId, new BridgeMappingsInput { Mappings = [.. copied] }, cancellationToken);
        }
        await workflow.PublishAsync(VersionKinds.BridgeVersion, version.BridgeVersionId, cancellationToken);
        return bridge.BridgeId;
    }
}
