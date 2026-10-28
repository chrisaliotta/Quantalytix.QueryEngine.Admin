using System.Text.RegularExpressions;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The row operations (P5, OI-48). <c>data</c> and <c>group</c> rows are members of the report dimension's one version, with a member
/// option when hidden, a header or styled; <c>section</c> and <c>formula</c> rows are overlay lines. A member row stays a member row
/// and a line a line; keys are fixed. The members are one tree with one root, the report's total, and sit under members; nothing sits
/// under a formula row. Deleting a row moves its children up and takes what names it with it (OI-56).
/// </summary>
internal static class ReportRowEdits
{
    /// <summary>Creates or updates a row and places it among its siblings.</summary>
    public static ReportGraph Save(ReportSnapshot snapshot, ReportSaveRowOperation save, ChangeContext context)
    {
        var report = snapshot.Content;
        var row = save.Row;
        var rows = ReportRowTree.Of(report);
        var stored = rows.GetValueOrDefault(row.Key);
        var isMember = ReportRowKinds.IsMember(row.Kind);
        if (stored is null && !Regex.IsMatch(row.Key, KeyRuleExtensions.KeyPattern))
            context.Reject("row.key", $"New row key '{row.Key}' must start with a letter and contain only letters, digits and '_'.");
        if (stored is { IsMember: true } && !isMember)
            context.Reject("row.kind", $"Row '{row.Key}' is a member of the report dimension, so it stays a data or group row.");
        if (stored is { IsMember: false } && isMember)
            context.Reject("row.kind", $"Row '{row.Key}' is an overlay row, so it stays a section or formula row.");
        if (row.Kind == ReportRowKinds.Group && report.SourceItems.Any(item => item.RowKey == row.Key))
            context.Reject("row.kind", $"Row '{row.Key}' takes items from the report's bridge sources, so it stays a data row; move its items first.");
        long? columnId = null;
        if (row.Kind == ReportRowKinds.Formula)
        {
            columnId = report.Columns.FirstOrDefault(column => column.ColumnKey == row.ValueColumnKey)?.ReportColumnId;
            if (columnId is null) context.Reject("row.valueColumnKey", $"Formula row '{row.Key}' places its value in '{row.ValueColumnKey}', which is no column of the report.");
        }
        if (!Placeable(rows, row.Key, isMember, row.ParentKey, "row.parentKey", context)) return report;

        var placed = isMember ? WithMember(snapshot, rows, stored?.Member, row, context) : WithLine(report, stored?.Line, row, columnId, context);
        placed = placed with
        {
            RowParameters =
            [
                .. placed.RowParameters.Where(value => value.RowKey != row.Key),
                .. row.Parameters.Select(value => RowParameterJson.Record(report.Report.ReportId, row.Key, value.Key, value.Value))
            ]
        };
        return Place(placed, row.Key, row.ParentKey, save.Index);
    }

    /// <summary>Places a row, with the rows under it, under a new parent.</summary>
    public static ReportGraph Move(ReportGraph report, ReportMoveRowOperation move, ChangeContext context)
    {
        var rows = ReportRowTree.Of(report);
        if (ReportEdits.Named(rows.Values, row => row.Key, move.Key, "row", context) is not { } moved) return report;
        if (!Placeable(rows, move.Key, moved.IsMember, move.To.ParentKey, "to.parentKey", context)) return report;
        var parentId = moved.Member is not null && move.To.ParentKey is { } parentKey ? rows[parentKey].Member!.DimensionMemberId : (long?)null;
        var reparented = report with
        {
            Members = [.. report.Members.Select(member => member.MemberKey == move.Key ? member with { ParentDimensionMemberId = parentId } : member)],
            Lines = [.. report.Lines.Select(line => line.LineKey == move.Key ? line with { ParentKey = move.To.ParentKey } : line)]
        };
        return Place(reparented, move.Key, move.To.ParentKey, move.To.Index);
    }

    /// <summary>Removes a row; its children take its place, and its option, parameter values, scopes and bridge rules go.</summary>
    public static ReportGraph Delete(ReportGraph report, ReportDeleteRowOperation delete, ChangeContext context)
    {
        var rows = ReportRowTree.Of(report);
        if (ReportEdits.Named(rows.Values, row => row.Key, delete.Key, "row", context) is not { } deleted) return report;
        if (deleted.IsMember && deleted.ParentKey is null)
        {
            context.Reject("key", $"Row '{delete.Key}' is the report's total, the one top-level member row; it stays.");
            return report;
        }

        var siblings = ReportRowTree.Children(rows, deleted.ParentKey).Select(row => row.Key).ToList();
        siblings.InsertRange(siblings.IndexOf(delete.Key), ReportRowTree.Children(rows, delete.Key).Select(row => row.Key));
        siblings.Remove(delete.Key);
        var parentId = deleted.Member?.ParentDimensionMemberId;
        var without = report with
        {
            Members =
            [
                .. report.Members
                    .Where(member => member.MemberKey != delete.Key)
                    .Select(member => deleted.Member is { } removed && member.ParentDimensionMemberId == removed.DimensionMemberId ? member with { ParentDimensionMemberId = parentId } : member)
            ],
            Lines = [.. report.Lines.Where(line => line.LineKey != delete.Key).Select(line => line.ParentKey == delete.Key ? line with { ParentKey = deleted.ParentKey } : line)],
            MemberOptions = [.. report.MemberOptions.Where(option => option.MemberKey != delete.Key)],
            RowParameters = [.. report.RowParameters.Where(value => value.RowKey != delete.Key)],
            Scopes = [.. report.Scopes.Where(scope => scope.ScopeMemberKey != delete.Key)],
            SourceItems = [.. report.SourceItems.Where(item => item.RowKey != delete.Key)]
        };
        return ReportRowTree.Reordered(without, siblings);
    }

    /// <summary>
    /// Whether a row may sit under a parent: the parent is a row, not a formula row, a member when the row is one, and not the row or
    /// a row under it; a member without a parent must be the root. Refuses the operation otherwise.
    /// </summary>
    private static bool Placeable(IReadOnlyDictionary<string, ReportRowTree> rows, string key, bool isMember, string? parentKey, string path, ChangeContext context)
    {
        if (parentKey is null)
        {
            if (isMember && rows.Values.Any(row => row.IsMember && row.ParentKey is null && row.Key != key))
                context.Reject(path, $"Row '{key}' is a member row, and the report has one top-level member row, its total; place it under a row.");
        }
        else if (rows.GetValueOrDefault(parentKey) is not { } parent)
            context.Reject(path, $"Row '{key}' sits under '{parentKey}', which is no row of the report.");
        else if (parent.Line?.LineKind == ReportRowKinds.Formula)
            context.Reject(path, $"Row '{key}' sits under formula row '{parentKey}', which has no rows under it.");
        else if (isMember && !parent.IsMember)
            context.Reject(path, $"Member row '{key}' sits under overlay row '{parentKey}'; member rows sit under member rows.");
        else if (rows.ContainsKey(key) && ReportRowTree.IsWithin(rows, parentKey, key))
            context.Reject(path, $"Row '{key}' would sit under itself.");
        return !context.Refused;
    }

    /// <summary>The report with a member row stored or updated, with its option.</summary>
    private static ReportGraph WithMember(ReportSnapshot snapshot, IReadOnlyDictionary<string, ReportRowTree> rows, DimensionMemberRecord? stored, ReportRow row, ChangeContext context)
    {
        var report = snapshot.Content;
        long? parentId = row.ParentKey is { } parentKey ? rows[parentKey].Member!.DimensionMemberId : null;
        var isAssignable = row.Kind == ReportRowKinds.Data;
        var member = stored is null
            ? new DimensionMemberRecord
            {
                DimensionMemberId = context.Ids.NewId(),
                DimensionVersionId = snapshot.Surroundings.DimensionVersionId,
                MemberKey = row.Key,
                DisplayName = row.Label,
                ParentDimensionMemberId = parentId,
                IsAssignable = isAssignable,
                SortOrder = 0
            }
            : stored with { DisplayName = row.Label, ParentDimensionMemberId = parentId, IsAssignable = isAssignable };
        ReportMemberOptionRecord[] option = !row.IsVisible || !row.ShowsValues || row.StyleKey is not null
            ? [new ReportMemberOptionRecord { ReportId = report.Report.ReportId, MemberKey = row.Key, IsVisible = row.IsVisible, ShowsValues = row.ShowsValues, StyleKey = row.StyleKey }]
            : [];
        return report with
        {
            Members = [.. report.Members.Where(other => other.MemberKey != row.Key), member],
            MemberOptions = [.. report.MemberOptions.Where(other => other.MemberKey != row.Key), .. option]
        };
    }

    /// <summary>The report with an overlay line stored or updated, keeping a stored line's pass-through payload, filter and settings.</summary>
    private static ReportGraph WithLine(ReportGraph report, ReportLineRecord? stored, ReportRow row, long? columnId, ChangeContext context)
    {
        var line = new ReportLineRecord
        {
            ReportLineId = stored?.ReportLineId ?? context.Ids.NewId(),
            ReportId = report.Report.ReportId,
            LineKey = row.Key,
            DisplayName = row.Label,
            LineKind = row.Kind,
            ParentKey = row.ParentKey,
            CalculatedMeasureKey = row.CalculatedMeasureKey,
            ValueReportColumnId = columnId,
            SortOrder = stored?.SortOrder ?? 0,
            IsVisible = row.IsVisible,
            PayloadJson = stored?.PayloadJson,
            FilterJson = stored?.FilterJson,
            SettingsJson = stored?.SettingsJson,
            StyleKey = row.StyleKey
        };
        return report with { Lines = [.. report.Lines.Where(other => other.LineKey != row.Key), line] };
    }

    /// <summary>The report with a row at an index among its parent's other children, the siblings renumbered.</summary>
    private static ReportGraph Place(ReportGraph report, string key, string? parentKey, int index)
    {
        var siblings = ReportRowTree.Children(ReportRowTree.Of(report), parentKey).Select(row => row.Key).Where(sibling => sibling != key).ToList();
        siblings.Insert(Math.Min(index, siblings.Count), key);
        return ReportRowTree.Reordered(report, siblings);
    }
}
