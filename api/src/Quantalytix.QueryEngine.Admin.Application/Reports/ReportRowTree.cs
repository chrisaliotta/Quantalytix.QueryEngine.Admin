using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// A report's rows as one tree: the report dimension's members, under their parent members, and the overlay lines, under the row
/// their parent key names. Siblings are ordered by sort order, a member before a line on a tie (P5).
/// </summary>
/// <param name="Key">The row key.</param>
/// <param name="ParentKey">The parent's key; null at the top level.</param>
/// <param name="Member">The member, for a member row.</param>
/// <param name="Line">The overlay line, for an overlay row.</param>
public sealed record ReportRowTree(string Key, string? ParentKey, DimensionMemberRecord? Member, ReportLineRecord? Line)
{
    /// <summary>Whether the row is a member of the report dimension.</summary>
    public bool IsMember => Member is not null;

    /// <summary>The order among siblings.</summary>
    private int SortOrder => Member?.SortOrder ?? Line!.SortOrder;

    /// <summary>Every row of a report, unordered.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The rows by key.</returns>
    public static IReadOnlyDictionary<string, ReportRowTree> Of(ReportGraph report)
    {
        var keyOf = report.Members.ToDictionary(member => member.DimensionMemberId, member => member.MemberKey);
        return report.Members
            .Select(member => new ReportRowTree(member.MemberKey, member.ParentDimensionMemberId is { } parentId ? keyOf[parentId] : null, member, null))
            .Concat(report.Lines.Select(line => new ReportRowTree(line.LineKey, line.ParentKey, null, line)))
            .ToDictionary(row => row.Key, StringComparer.Ordinal);
    }

    /// <summary>The children of a row, in order.</summary>
    /// <param name="rows">Every row.</param>
    /// <param name="parentKey">The parent's key; null for the top level.</param>
    /// <returns>The children.</returns>
    public static IReadOnlyList<ReportRowTree> Children(IReadOnlyDictionary<string, ReportRowTree> rows, string? parentKey)
    {
        return
        [
            .. rows.Values
                .Where(row => row.ParentKey == parentKey)
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.IsMember ? 0 : 1)
                .ThenBy(row => row.Key, StringComparer.Ordinal)
        ];
    }

    /// <summary>Every row in hierarchy order: each row, then the rows under it.</summary>
    /// <param name="rows">Every row.</param>
    /// <returns>The rows in order.</returns>
    public static IEnumerable<ReportRowTree> InOrder(IReadOnlyDictionary<string, ReportRowTree> rows)
    {
        return Walk(rows, null);
    }

    /// <summary>Whether a row is a descendant of another, or the row itself.</summary>
    /// <param name="rows">Every row.</param>
    /// <param name="key">The row.</param>
    /// <param name="ancestorKey">The candidate ancestor.</param>
    /// <returns>True when walking up from the row reaches the ancestor.</returns>
    public static bool IsWithin(IReadOnlyDictionary<string, ReportRowTree> rows, string key, string ancestorKey)
    {
        for (string? current = key; current is not null; current = rows[current].ParentKey)
            if (current == ancestorKey) return true;
        return false;
    }

    /// <summary>A report whose rows under a parent take the given order, numbering their sort orders from zero.</summary>
    /// <param name="report">The report.</param>
    /// <param name="orderedKeys">The parent's children, in their new order.</param>
    /// <returns>The report with the children's sort orders rewritten.</returns>
    public static ReportGraph Reordered(ReportGraph report, IReadOnlyList<string> orderedKeys)
    {
        var position = orderedKeys.Select((key, index) => (key, index)).ToDictionary(entry => entry.key, entry => entry.index, StringComparer.Ordinal);
        return report with
        {
            Members = [.. report.Members.Select(member => position.TryGetValue(member.MemberKey, out var index) ? member with { SortOrder = index } : member)],
            Lines = [.. report.Lines.Select(line => position.TryGetValue(line.LineKey, out var index) ? line with { SortOrder = index } : line)]
        };
    }

    /// <summary>The rows under a parent, depth first.</summary>
    private static IEnumerable<ReportRowTree> Walk(IReadOnlyDictionary<string, ReportRowTree> rows, string? parentKey)
    {
        foreach (var row in Children(rows, parentKey))
        {
            yield return row;
            foreach (var descendant in Walk(rows, row.Key)) yield return descendant;
        }
    }
}
