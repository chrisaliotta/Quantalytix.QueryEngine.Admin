using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// The shape a report's row tree must have before anything is written: unique keys, one top-level member row, which is the
/// report's total and the root the engine evaluates formula rows at, every parent a row of the tree, member rows under member rows
/// because the report dimension's hierarchy holds them, nothing under a formula row, and no row under itself. A data row may have
/// member rows under it, as an assignable member with children does: its total is its own items and the rows under it.
/// </summary>
public static class ReportRowTree
{
    /// <summary>The problems of a row tree; none when its shape is valid.</summary>
    /// <param name="rows">The complete row tree.</param>
    /// <returns>One message per problem.</returns>
    public static IEnumerable<string> Problems(IReadOnlyList<ReportRowInput> rows)
    {
        var byKey = new Dictionary<string, ReportRowInput>(StringComparer.Ordinal);
        foreach (var row in rows)
            if (!byKey.TryAdd(row.Key, row)) yield return $"Row key '{row.Key}' appears more than once.";

        var topMembers = rows.Count(row => ReportRowKinds.IsMember(row.Kind) && row.ParentKey is null);
        if (topMembers != 1) yield return $"The report has {topMembers} top-level member rows; it needs exactly one, the report's total.";

        foreach (var row in rows.Where(row => row.ParentKey is not null))
        {
            if (!byKey.TryGetValue(row.ParentKey!, out var parent))
                yield return $"Row '{row.Key}' sits under '{row.ParentKey}', which is no row of the report.";
            else if (parent.Kind == ReportRowKinds.Formula)
                yield return $"Row '{row.Key}' sits under formula row '{parent.Key}', which has no rows under it.";
            else if (ReportRowKinds.IsMember(row.Kind) && !ReportRowKinds.IsMember(parent.Kind))
                yield return $"Member row '{row.Key}' sits under overlay row '{parent.Key}'; member rows sit under member rows.";
        }

        foreach (var row in rows.Where(row => UnderItself(row, byKey)))
            yield return $"Row '{row.Key}' sits under itself through its parents.";
    }

    /// <summary>Whether walking up from a row's parent reaches the row again.</summary>
    private static bool UnderItself(ReportRowInput row, IReadOnlyDictionary<string, ReportRowInput> byKey)
    {
        var current = row.ParentKey;
        for (var steps = 0; current is not null && steps < byKey.Count; steps++)
        {
            if (current == row.Key) return true;
            current = byKey.GetValueOrDefault(current)?.ParentKey;
        }
        return false;
    }
}
