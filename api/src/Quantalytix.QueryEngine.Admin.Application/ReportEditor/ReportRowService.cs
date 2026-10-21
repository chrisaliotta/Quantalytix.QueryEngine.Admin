using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Replaces a report's row tree. Member rows become members of the report dimension's one version, with member options for the
/// hidden rows and headers, and overlay rows the report's overlay rows. A row the tree leaves out goes with what names it: the
/// bridge entries that map items to it, its variable scopes and its member option. The rows are composed first, so a write to an
/// enabled report is checked before anything is written, and the version's closure is rebuilt once the members are.
/// </summary>
public sealed class ReportRowService(
    ITableGateway<ReportRecord> reports,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportSourceRecord> sources,
    BridgeMappingService bridgeMappings,
    OwnedVersions owned,
    ClosureBuilder closure,
    ReportChanges changes,
    IIdGenerator ids)
{
    /// <summary>Replaces the report's rows with the given tree.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The complete, validated row tree.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The violations that kept an enabled report from taking the tree, in which case nothing was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> ReplaceAsync(long reportId, ReportRowsInput input, CancellationToken cancellationToken)
    {
        var owner = new { ReportId = reportId };
        var report = await reports.GetAsync(owner, cancellationToken);
        var structureId = (await owned.StructureAsync(report, cancellationToken)).DimensionVersionId;
        var stored = (await members.ListAsync(new { DimensionVersionId = structureId }, cancellationToken)).ToDictionary(member => member.MemberKey, StringComparer.Ordinal);
        var memberRows = input.Rows.Where(row => ReportRowKinds.IsMember(row.Kind)).ToList();
        var memberIds = memberRows.ToDictionary(
            row => row.Key, row => stored.TryGetValue(row.Key, out var member) ? member.MemberId : ids.NextId(), StringComparer.Ordinal);
        DimensionMemberRecord[] tree = [.. memberRows.Select(row => Member(structureId, stored.GetValueOrDefault(row.Key), memberIds, row))];
        var removed = stored.Values.Where(member => !memberIds.ContainsKey(member.MemberKey)).ToList();
        var removedKeys = removed.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);

        var storedLines = (await lines.ListAsync(owner, cancellationToken)).ToDictionary(line => line.LineKey, StringComparer.Ordinal);
        var columnIds = (await columns.ListAsync(owner, cancellationToken)).ToDictionary(column => column.ColumnKey, column => column.ReportColumnId, StringComparer.Ordinal);
        ReportLineRecord[] overlay =
            [.. input.Rows.Where(row => !ReportRowKinds.IsMember(row.Kind)).Select(row => Line(reportId, storedLines.GetValueOrDefault(row.Key), columnIds, row))];
        ReportMemberOptionRecord[] options =
        [
            .. memberRows.Where(row => !row.IsVisible || !row.ShowsValues)
                .Select(row => new ReportMemberOptionRecord { ReportId = reportId, MemberKey = row.Key, IsVisible = row.IsVisible, ShowsValues = row.ShowsValues })
        ];
        var storedScopes = await scopes.ListAsync(owner, cancellationToken);
        ReportVariableScopeRecord[] keptScopes = [.. storedScopes.Where(scope => !removedKeys.Contains(scope.ScopeMemberKey))];

        return await changes.ApplyAsync(
            reportId,
            graph => graph with
            {
                Members = tree,
                Lines = overlay,
                MemberOptions = options,
                Scopes = keptScopes,
                SourceItems = [.. graph.SourceItems.Where(item => !removedKeys.Contains(item.RowKey))]
            },
            async () =>
            {
                await RemoveEntriesToAsync(reportId, removedKeys, cancellationToken);
                if (keptScopes.Length != storedScopes.Count)
                {
                    await scopes.DeleteAsync(owner, cancellationToken);
                    await scopes.InsertManyAsync(keptScopes, cancellationToken);
                }
                await memberOptions.DeleteAsync(owner, cancellationToken);
                await memberOptions.InsertManyAsync(options, cancellationToken);
                long[] droppedLines = [.. storedLines.Values.Select(line => line.ReportLineId).Except(overlay.Select(line => line.ReportLineId))];
                await lines.DeleteAsync(new { ReportLineId = droppedLines }, cancellationToken);
                await lines.InsertManyAsync(overlay, cancellationToken);
                await members.InsertManyAsync([.. tree.Where(member => member != stored.GetValueOrDefault(member.MemberKey))], cancellationToken);
                await members.DeleteAsync(new { MemberId = removed.Select(member => member.MemberId).ToArray() }, cancellationToken);
                await closure.RebuildAsync(structureId, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>Removes the entries of the bridge sources' bridges that map items to removed rows.</summary>
    private async Task RemoveEntriesToAsync(long reportId, IReadOnlySet<string> removedKeys, CancellationToken cancellationToken)
    {
        if (removedKeys.Count == 0) return;
        var bridgeVersions = await owned.BridgesAsync(await sources.ListAsync(new { ReportId = reportId }, cancellationToken), cancellationToken);
        foreach (var bridgeVersion in bridgeVersions.Values)
        {
            var entries = await bridgeMappings.ListAsync(bridgeVersion.BridgeVersionId, cancellationToken);
            if (!entries.Any(entry => removedKeys.Contains(entry.TargetMemberKey))) continue;
            var kept = entries.Where(entry => !removedKeys.Contains(entry.TargetMemberKey)).Select(BridgeEntries.Kept);
            await bridgeMappings.ReplaceAsync(bridgeVersion.BridgeVersionId, new BridgeMappingsInput { Mappings = [.. kept] }, cancellationToken);
        }
    }

    /// <summary>The overlay row a row of the tree describes, keeping a stored row's ID and pass-through payload, filter and settings.</summary>
    private ReportLineRecord Line(long reportId, ReportLineRecord? stored, IReadOnlyDictionary<string, long> columnIds, ReportRowInput row)
    {
        return new ReportLineRecord
        {
            ReportLineId = stored?.ReportLineId ?? ids.NextId(),
            ReportId = reportId,
            LineKey = row.Key,
            DisplayName = row.Label,
            LineKind = row.Kind,
            ParentKey = row.ParentKey,
            CalculatedMeasureKey = row.CalculatedMeasureKey,
            ValueColumnId = row.ValueColumnKey is { } column ? columnIds[column] : null,
            SortOrder = row.SortOrder,
            IsVisible = row.IsVisible,
            PayloadJson = stored?.PayloadJson,
            FilterJson = stored?.FilterJson,
            SettingsJson = stored?.SettingsJson
        };
    }

    /// <summary>
    /// The member a member row describes: a stored member with its label, place and kind, or a new one. A data row is assignable
    /// and a group row is not.
    /// </summary>
    private static DimensionMemberRecord Member(long structureId, DimensionMemberRecord? stored, IReadOnlyDictionary<string, long> memberIds, ReportRowInput row)
    {
        long? parentId = row.ParentKey is { } parentKey ? memberIds[parentKey] : null;
        var isAssignable = row.Kind == ReportRowKinds.Data;
        return stored is null
            ? new DimensionMemberRecord
            {
                MemberId = memberIds[row.Key],
                DimensionVersionId = structureId,
                MemberKey = row.Key,
                DisplayName = row.Label,
                ParentMemberId = parentId,
                IsAssignable = isAssignable,
                SortOrder = row.SortOrder
            }
            : stored with { DisplayName = row.Label, ParentMemberId = parentId, IsAssignable = isAssignable, SortOrder = row.SortOrder };
    }
}
