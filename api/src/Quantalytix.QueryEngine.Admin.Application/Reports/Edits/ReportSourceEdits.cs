using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The source operations (P6, P7). A source's dataset, method, role and field are fixed once created. A bridge source owns its
/// report bridge, which the commit creates with the source and deletes with it; its rules are the bridge's mappings, one list per
/// data row, and its left-out items are excluded from coverage on purpose (OI-49, OI-51). Deleting a source clears the profile picks
/// that name it (OI-56).
/// </summary>
internal static class ReportSourceEdits
{
    /// <summary>Creates a source, optionally as a copy of another's content, or updates a stored one's name and time field.</summary>
    public static ReportGraph Save(ReportSnapshot snapshot, ReportSaveSourceOperation save, ChangeContext context)
    {
        var report = snapshot.Content;
        var input = save.Input;
        var stored = report.Sources.FirstOrDefault(source => source.SourceKey == input.Key);
        if (stored is not null)
        {
            if (save.CopyFromSourceKey is not null) context.Reject("copyFromSourceKey", $"Source '{input.Key}' exists; only a new source starts as a copy.");
            if (stored.DatasetId != input.DatasetId || stored.MappingMethod != input.Method || stored.DimensionRoleId != input.RoleId || stored.FieldKey != input.FieldKey)
                context.Reject("input", $"Source '{input.Key}' keeps its dataset, method, role and field; they are fixed once created.");
            if (context.Refused) return report;
            var updated = stored with { DisplayName = input.DisplayName, TimeFieldKey = input.TimeFieldKey };
            return report with { Sources = [.. report.Sources.Where(source => source != stored), updated] };
        }

        if (!snapshot.Surroundings.DatasetIds.Contains(input.DatasetId)) context.Reject("input.datasetId", $"Dataset {input.DatasetId} does not exist.");
        if (input.RoleId is { } roleId && snapshot.Surroundings.Roles.All(role => role.DimensionRoleId != roleId)) context.Reject("input.roleId", $"Role {roleId} does not exist.");
        if (input.Method == ReportMappingMethods.Bridge && ReportSourceKeys.Bridge(report.ReportKey, input.Key).Length > 128)
            context.Reject("input.key", $"The source's bridge takes the report's key and '{input.Key}' as its key, which is longer than 128 characters.");
        var copied = save.CopyFromSourceKey is { } copyFrom ? ReportEdits.Named(report.Sources, source => source.SourceKey, copyFrom, "source", context) : null;
        if (context.Refused) return report;

        var created = new ReportSourceRecord
        {
            ReportSourceId = context.Ids.NewId(),
            ReportId = report.Report.ReportId,
            SourceKey = input.Key,
            DisplayName = input.DisplayName,
            DatasetId = input.DatasetId,
            MappingMethod = input.Method,
            FieldKey = input.FieldKey,
            DimensionRoleId = input.RoleId,
            TimeFieldKey = input.TimeFieldKey
        };
        var withSource = report with { Sources = [.. report.Sources, created] };
        if (copied is null) return withSource;
        if (!SameItems(snapshot, copied, created))
        {
            context.Reject("copyFromSourceKey", $"Source '{copied.SourceKey}' takes items of another dimension, or none; a copy takes rules from a bridge source over the same dimension.");
            return report;
        }

        return withSource with
        {
            SourceVariables = [.. report.SourceVariables, .. report.SourceVariables.Where(variable => variable.ReportSourceId == copied.ReportSourceId).Select(variable => variable with { ReportSourceId = created.ReportSourceId })],
            SourceItems = [.. report.SourceItems, .. report.SourceItems.Where(item => item.ReportSourceId == copied.ReportSourceId).Select(item => item with { ReportSourceId = created.ReportSourceId })],
            LeftOut = [.. report.LeftOut, .. report.LeftOut.Where(item => item.ReportSourceId == copied.ReportSourceId).Select(item => item with { ReportSourceId = created.ReportSourceId })]
        };
    }

    /// <summary>Removes a source with its variables, rules, left-out items and the profile picks that name it.</summary>
    public static ReportGraph Delete(ReportGraph report, ReportDeleteSourceOperation delete, ChangeContext context)
    {
        if (ReportEdits.Named(report.Sources, source => source.SourceKey, delete.Key, "source", context) is not { } source) return report;
        var id = source.ReportSourceId;
        return report with
        {
            Sources = [.. report.Sources.Where(other => other != source)],
            SourceVariables = [.. report.SourceVariables.Where(variable => variable.ReportSourceId != id)],
            SourceItems = [.. report.SourceItems.Where(item => item.ReportSourceId != id)],
            LeftOut = [.. report.LeftOut.Where(item => item.ReportSourceId != id)],
            Picks = [.. report.Picks.Where(pick => pick.ReportSourceId != id)]
        };
    }

    /// <summary>Replaces a source's variables; the profile picks of the source for variables it no longer supplies go.</summary>
    public static ReportGraph SetVariables(ReportGraph report, ReportSetSourceVariablesOperation set, ChangeContext context)
    {
        if (ReportEdits.Named(report.Sources, source => source.SourceKey, set.SourceKey, "source", context) is not { } source) return report;
        var id = source.ReportSourceId;
        var supplied = set.Variables.Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal);
        return report with
        {
            SourceVariables =
            [
                .. report.SourceVariables.Where(variable => variable.ReportSourceId != id),
                .. set.Variables.Select(variable => new ReportSourceVariableRecord
                {
                    ReportSourceId = id,
                    VariableKey = variable.VariableKey,
                    MeasureKey = variable.MeasureKey,
                    SourceFilterJson = ReportFilters.ToEngine(variable.Filter),
                    MissingBehavior = variable.MissingBehavior
                })
            ],
            Picks = [.. report.Picks.Where(pick => pick.ReportSourceId != id || supplied.Contains(pick.VariableKey))]
        };
    }

    /// <summary>Replaces the rules of one data row of a bridge source.</summary>
    public static ReportGraph SetRowRules(ReportGraph report, ReportSetRowRulesOperation set, ChangeContext context)
    {
        if (BridgeSource(report, set.SourceKey, context) is not { } source) return report;
        if (ReportEdits.Named(ReportRowTree.Of(report).Values, row => row.Key, set.RowKey, "row", context) is not { } row) return report;
        if (row.Member is not { IsAssignable: true })
        {
            context.Reject("rowKey", $"Row '{set.RowKey}' is no data row of the report; only data rows take items.");
            return report;
        }

        return report with
        {
            SourceItems =
            [
                .. report.SourceItems.Where(item => item.ReportSourceId != source.ReportSourceId || item.RowKey != set.RowKey),
                .. set.Rules.Select(rule => new ReportSourceItem
                {
                    ReportSourceId = source.ReportSourceId, ItemKey = rule.ItemKey, Selection = rule.Selection, RowKey = set.RowKey, Factor = rule.Factor
                })
            ]
        };
    }

    /// <summary>Replaces the items a bridge source leaves out on purpose.</summary>
    public static ReportGraph SetLeftOut(ReportGraph report, ReportSetLeftOutOperation set, ChangeContext context)
    {
        if (BridgeSource(report, set.SourceKey, context) is not { } source) return report;
        return report with
        {
            LeftOut =
            [
                .. report.LeftOut.Where(item => item.ReportSourceId != source.ReportSourceId),
                .. set.ItemKeys.Select(itemKey => new ReportSourceLeftOutRecord { ReportSourceId = source.ReportSourceId, MemberKey = itemKey })
            ]
        };
    }

    /// <summary>The bridge source with a key; refuses a direct source, and a key the report lacks is a conflict.</summary>
    private static ReportSourceRecord? BridgeSource(ReportGraph report, string sourceKey, ChangeContext context)
    {
        if (ReportEdits.Named(report.Sources, source => source.SourceKey, sourceKey, "source", context) is not { } source) return null;
        if (source.DimensionRoleId is not null) return source;
        context.Reject("sourceKey", $"Source '{sourceKey}' is a direct source; its field's values name its rows, so it has no items.");
        return null;
    }

    /// <summary>Whether two sources take items of the same dimension, or neither takes items, so one's rules fit the other.</summary>
    private static bool SameItems(ReportSnapshot snapshot, ReportSourceRecord copied, ReportSourceRecord created)
    {
        long? DimensionOf(ReportSourceRecord source) =>
            source.DimensionRoleId is { } roleId ? snapshot.Surroundings.Roles.Single(role => role.DimensionRoleId == roleId).DimensionId : null;
        return DimensionOf(copied) == DimensionOf(created);
    }
}
