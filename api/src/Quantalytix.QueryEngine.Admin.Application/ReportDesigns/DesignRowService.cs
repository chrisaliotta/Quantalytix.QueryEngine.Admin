using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Replaces a report design's row tree. Member rows become members of the report dimension's paired version, with member options
/// for the hidden rows and headers, and overlay rows the report version's overlay rows. A row the tree leaves out goes after what
/// names it: its bridge entries, its variable scopes and its member option, then the member, the rows under it first.
/// </summary>
public sealed class DesignRowService(
    ReportDesignVersions designs,
    ReportDesignReader reader,
    DimensionMemberService members,
    ReportMemberOptionService memberOptions,
    ReportLineService lineService,
    ReportVariableScopeService scopeService,
    BridgeMappingService bridgeMappings,
    ITableGateway<ReportLineRecord> lines)
{
    /// <summary>Replaces the design's rows with the given tree.</summary>
    /// <param name="reportVersionId">A draft or published report version.</param>
    /// <param name="input">The complete, validated row tree.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The design afterwards.</returns>
    public async Task<ReportDesign> ReplaceAsync(long reportVersionId, DesignRowsInput input, CancellationToken cancellationToken)
    {
        var scope = await designs.ReadAsync(reportVersionId, cancellationToken);
        var structureId = scope.Structure.DimensionVersionId;
        var stored = (await members.ListAsync(structureId, cancellationToken)).ToDictionary(member => member.MemberKey, StringComparer.Ordinal);
        var memberRows = input.Rows.Where(row => DesignRowKinds.IsMember(row.Kind)).ToList();
        var keptKeys = memberRows.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        var removed = stored.Values.Where(member => !keptKeys.Contains(member.MemberKey)).ToList();
        var removedKeys = removed.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);

        await RemoveMappingsToAsync(scope, removedKeys, cancellationToken);
        await RemoveScopesOnAsync(reportVersionId, removedKeys, cancellationToken);
        var hiddenOrHeaders = memberRows.Where(row => !row.IsVisible || !row.ShowsValues);
        await memberOptions.ReplaceAsync(
            reportVersionId,
            new ReportMemberOptionsInput
            {
                Options = [.. hiddenOrHeaders.Select(row => new ReportMemberOptionInput { MemberKey = row.Key, IsVisible = row.IsVisible, ShowsValues = row.ShowsValues })]
            },
            cancellationToken);
        await ReplaceOverlayRowsAsync(reportVersionId, input.Rows, cancellationToken);

        var ids = await WriteMembersAsync(structureId, stored, memberRows, cancellationToken);
        var placements = memberRows.Select(row => new MemberPlacement
        {
            MemberId = ids[row.Key],
            ParentMemberId = row.ParentKey is { } parentKey ? ids[parentKey] : null,
            SortOrder = row.SortOrder
        });
        await members.ReorderAsync(structureId, new MemberOrderInput { Items = [.. placements] }, cancellationToken);
        foreach (var member in DeepestFirst(stored.Values, removed))
            await members.DeleteAsync(new MemberReference { DimensionVersionId = structureId, MemberId = member.MemberId }, cancellationToken);
        return await reader.ReadAsync(reportVersionId, cancellationToken);
    }

    /// <summary>Removes the entries of the paired bridge versions that map items to removed rows.</summary>
    private async Task RemoveMappingsToAsync(ReportDesignScope scope, IReadOnlySet<string> removedKeys, CancellationToken cancellationToken)
    {
        foreach (var bridgeVersion in scope.BridgeVersions.Values)
        {
            var entries = await bridgeMappings.ListAsync(bridgeVersion.BridgeVersionId, cancellationToken);
            if (!entries.Any(entry => removedKeys.Contains(entry.TargetMemberKey))) continue;
            var kept = entries.Where(entry => !removedKeys.Contains(entry.TargetMemberKey)).Select(BridgeEntries.Kept);
            await bridgeMappings.ReplaceAsync(bridgeVersion.BridgeVersionId, new BridgeMappingsInput { Mappings = [.. kept] }, cancellationToken);
        }
    }

    /// <summary>Removes the variable scopes that name removed rows.</summary>
    private async Task RemoveScopesOnAsync(long reportVersionId, IReadOnlySet<string> removedKeys, CancellationToken cancellationToken)
    {
        var stored = await scopeService.ListAsync(reportVersionId, cancellationToken);
        if (!stored.Any(scope => removedKeys.Contains(scope.ScopeMemberKey))) return;
        var kept = stored
            .Where(scope => !removedKeys.Contains(scope.ScopeMemberKey))
            .Select(scope => new ReportVariableScopeInput { VariableKey = scope.VariableKey, ScopeMemberKey = scope.ScopeMemberKey, SelectionScope = scope.SelectionScope });
        await scopeService.ReplaceAsync(reportVersionId, new ReportVariableScopesInput { Scopes = [.. kept] }, cancellationToken);
    }

    /// <summary>Replaces the overlay rows, keeping each stored row's ID and pass-through payload and settings.</summary>
    private async Task ReplaceOverlayRowsAsync(long reportVersionId, IReadOnlyList<DesignRowInput> rows, CancellationToken cancellationToken)
    {
        var stored = (await lines.ListAsync(new { ReportVersionId = reportVersionId }, cancellationToken)).ToDictionary(line => line.LineKey, StringComparer.Ordinal);
        var overlayRows = ParentsFirst([.. rows.Where(row => !DesignRowKinds.IsMember(row.Kind))]).Select(row =>
        {
            var line = stored.GetValueOrDefault(row.Key);
            return new ReportLineInput
            {
                ReportLineId = line?.ReportLineId,
                LineKey = row.Key,
                DisplayName = row.Label,
                LineKind = row.Kind,
                ParentKey = row.ParentKey,
                CalculatedMeasureKey = row.CalculatedMeasureKey,
                ValueColumnKey = row.ValueColumnKey,
                SortOrder = row.SortOrder,
                IsVisible = row.IsVisible,
                PayloadJson = line?.PayloadJson,
                SettingsJson = line?.SettingsJson
            };
        });
        await lineService.ReplaceAsync(reportVersionId, new ReportLinesInput { Lines = [.. overlayRows] }, cancellationToken);
    }

    /// <summary>
    /// Creates the new member rows and updates the stored ones whose label or kind changed, parents first, so each new member's
    /// parent exists. A data row is assignable and a group row is not. Returns every member row's member ID by key.
    /// </summary>
    private async Task<Dictionary<string, long>> WriteMembersAsync(
        long structureId,
        IReadOnlyDictionary<string, DimensionMemberRecord> stored,
        IReadOnlyList<DesignRowInput> memberRows,
        CancellationToken cancellationToken)
    {
        var ids = stored.Values.ToDictionary(member => member.MemberKey, member => member.MemberId, StringComparer.Ordinal);
        foreach (var row in ParentsFirst(memberRows))
        {
            long? parentId = row.ParentKey is { } parentKey ? ids[parentKey] : null;
            var isAssignable = row.Kind == DesignRowKinds.Data;
            if (stored.TryGetValue(row.Key, out var member))
            {
                if (member.DisplayName == row.Label && member.IsAssignable == isAssignable) continue;
                var edited = new MemberInput
                {
                    DisplayName = row.Label,
                    Description = member.Description,
                    ParentMemberId = parentId,
                    LevelId = member.LevelId,
                    IsAssignable = isAssignable,
                    SortOrder = row.SortOrder,
                    Properties = member.Properties
                };
                await members.UpdateAsync(new MemberReference { DimensionVersionId = structureId, MemberId = member.MemberId }, edited, cancellationToken);
                continue;
            }

            var created = new NewMemberInput { MemberKey = row.Key, DisplayName = row.Label, ParentMemberId = parentId, IsAssignable = isAssignable, SortOrder = row.SortOrder };
            ids[row.Key] = (await members.CreateAsync(structureId, created, cancellationToken)).MemberId;
        }
        return ids;
    }

    /// <summary>The rows in an order that puts each row after the row it sits under, when that row is in the list too.</summary>
    private static IEnumerable<DesignRowInput> ParentsFirst(IReadOnlyList<DesignRowInput> rows)
    {
        var keys = rows.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        var children = rows.ToLookup(row => row.ParentKey is { } parentKey && keys.Contains(parentKey) ? parentKey : null);
        IEnumerable<DesignRowInput> Under(string? parentKey) => children[parentKey].SelectMany(row => Under(row.Key).Prepend(row));
        return Under(null);
    }

    /// <summary>The removed members, each after the removed members under it in the stored tree.</summary>
    private static IEnumerable<DimensionMemberRecord> DeepestFirst(IEnumerable<DimensionMemberRecord> stored, IEnumerable<DimensionMemberRecord> removed)
    {
        var byId = stored.ToDictionary(member => member.MemberId);
        int Depth(DimensionMemberRecord member) => member.ParentMemberId is { } parentId ? Depth(byId[parentId]) + 1 : 0;
        return removed.OrderByDescending(Depth);
    }
}
