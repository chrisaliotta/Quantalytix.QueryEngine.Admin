using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Plans a report commit's source rows and the bridge sources' report bridges (P7, B9, OI-49), which are written only through the
/// mappings module's <see cref="ReportBridges"/>. A new bridge source's bridge is created (live version 1 into the report dimension)
/// before its row is stored with the bridge's id; a renamed source renames its bridge; a deleted source's row goes before its bridge,
/// which takes its rules. A commit that stops between them leaves at most a bridge no source owns, which the engine never reads and
/// the bridges module may delete. A source's rules into one row are replaced as a whole, after the sources that own them.
/// </summary>
/// <param name="sources">The report source table.</param>
/// <param name="bridges">Writes the report bridges.</param>
public sealed class ReportSourceWrites(ITableGateway<ReportSourceRecord> sources, ReportBridges bridges)
{
    /// <summary>Plans the source rows at one depth and their rules one depth below.</summary>
    /// <param name="before">The report as stored.</param>
    /// <param name="after">The report with every operation applied.</param>
    /// <param name="depth">The source rows' depth below the report row.</param>
    /// <param name="plan">The plan.</param>
    public void Plan(ReportSnapshot before, ReportSnapshot after, int depth, WritePlan plan)
    {
        var stored = before.Content.Sources.ToDictionary(source => source.ReportSourceId);
        var changed = after.Content.Sources.ToDictionary(source => source.ReportSourceId);
        // Filled with new sources' bridges by their inserts, which run before the rule writes one depth below.
        var bridgeOf = stored.Values.Where(source => source.BridgeId is not null).ToDictionary(source => source.ReportSourceId, source => source.BridgeId!.Value);

        foreach (var source in stored.Values.Where(source => !changed.ContainsKey(source.ReportSourceId)))
        {
            plan.Delete(sources, depth, new { source.ReportSourceId });
            if (source.BridgeId is { } bridgeId) plan.Call(WriteKind.Delete, depth, cancellationToken => bridges.DeleteAsync(bridgeId, cancellationToken));
        }

        foreach (var source in changed.Values.Where(source => stored.TryGetValue(source.ReportSourceId, out var original) && original != source))
        {
            plan.Update(sources, depth, source);
            if (source.BridgeId is { } bridgeId && source.DisplayName != stored[source.ReportSourceId].DisplayName)
                plan.Call(WriteKind.Update, depth, cancellationToken => bridges.RenameAsync(bridgeId, source.DisplayName, cancellationToken));
        }

        ReportSourceRecord[] created = [.. changed.Values.Where(source => !stored.ContainsKey(source.ReportSourceId))];
        if (created.Length > 0) plan.Call(WriteKind.Insert, depth, cancellationToken => InsertAsync(created, after, bridgeOf, cancellationToken));
        PlanRules(before.Content, after.Content, depth + 1, bridgeOf, plan);
    }

    /// <summary>Stores new sources, and first each bridge source's bridge.</summary>
    private async Task InsertAsync(
        IReadOnlyList<ReportSourceRecord> created,
        ReportSnapshot after,
        Dictionary<long, long> bridgeOf,
        CancellationToken cancellationToken)
    {
        var report = after.Content.Report;
        var rows = new List<ReportSourceRecord>();
        foreach (var source in created)
        {
            if (source.DimensionRoleId is not { } roleId)
            {
                rows.Add(source);
                continue;
            }

            var bridgeId = await bridges.CreateAsync(
                new ReportBridgeInput
                {
                    Key = ReportSourceKeys.Bridge(report.ReportKey, source.SourceKey),
                    DisplayName = source.DisplayName,
                    SourceDimensionId = after.Surroundings.Roles.Single(role => role.DimensionRoleId == roleId).DimensionId,
                    ReportDimensionId = report.ReportDimensionId
                },
                cancellationToken);
            bridgeOf[source.ReportSourceId] = bridgeId;
            rows.Add(source with { BridgeId = bridgeId });
        }

        await sources.InsertManyAsync(rows, cancellationToken);
    }

    /// <summary>
    /// Plans the rules of the bridge sources that stay or are new: a row whose rules changed is replaced, a row that lost its rules is
    /// emptied, and a row the report no longer has loses its rules. A deleted source's rules go with its bridge.
    /// </summary>
    private void PlanRules(ReportGraph stored, ReportGraph changed, int depth, Dictionary<long, long> bridgeOf, WritePlan plan)
    {
        var kept = changed.Sources.Select(source => source.ReportSourceId).ToHashSet();
        var before = Rules(stored, kept);
        var after = Rules(changed, kept);
        var rowKeys = changed.Members.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);
        foreach (var key in before.Keys.Where(key => !after.ContainsKey(key)))
        {
            if (rowKeys.Contains(key.Row)) Replace(key, [], depth, bridgeOf, plan);
            else plan.Call(WriteKind.Delete, depth, cancellationToken => bridges.RemoveRowsAsync(bridgeOf[key.Source], [key.Row], cancellationToken));
        }

        foreach (var (key, rules) in after.Where(row => !before.TryGetValue(row.Key, out var rules) || !rules.ToHashSet().SetEquals(row.Value)))
            Replace(key, rules, depth, bridgeOf, plan);
    }

    /// <summary>Plans the replacement of one source's rules into one row.</summary>
    private void Replace((long Source, string Row) key, IReadOnlyList<ReportBridgeRule> rules, int depth, Dictionary<long, long> bridgeOf, WritePlan plan)
    {
        plan.Call(WriteKind.Insert, depth, cancellationToken => bridges.ReplaceRowAsync(bridgeOf[key.Source], key.Row, rules, cancellationToken));
    }

    /// <summary>The rules of the kept sources, by source and row.</summary>
    private static Dictionary<(long Source, string Row), IReadOnlyList<ReportBridgeRule>> Rules(ReportGraph report, IReadOnlySet<long> kept)
    {
        return report.SourceItems
            .Where(item => kept.Contains(item.ReportSourceId))
            .GroupBy(item => (Source: item.ReportSourceId, Row: item.RowKey))
            .ToDictionary(
                row => row.Key,
                row => (IReadOnlyList<ReportBridgeRule>)[.. row.Select(item => new ReportBridgeRule { ItemKey = item.ItemKey, Selection = item.Selection, Factor = item.Factor })]);
    }
}
