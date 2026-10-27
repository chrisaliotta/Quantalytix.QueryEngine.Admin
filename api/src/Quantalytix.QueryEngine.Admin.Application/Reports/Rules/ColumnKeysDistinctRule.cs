using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>column_keys_distinct</c>: the report returns at least one column, and no column has the key of an enabled calculation
/// dimension, because a report table's schema names both by key.
/// </summary>
public sealed class ColumnKeysDistinctRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "column_keys_distinct";

    /// <summary>Reports a report without columns, and each column named like a calculation dimension.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Unused: columns and calculation dimensions are the report's own.</param>
    /// <returns>The violations found.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        if (report.Columns.Count == 0) yield return Violation($"Report '{report.ReportKey}' has no columns, so it returns nothing.");
        var dimensionKeys = report.EnabledDimensions.Select(dimension => dimension.DimensionKey).ToHashSet(StringComparer.Ordinal);
        foreach (var column in report.Columns.Where(column => dimensionKeys.Contains(column.ColumnKey)))
            yield return Violation($"Report '{report.ReportKey}' column '{column.ColumnKey}' is named like a calculation dimension of the report.");
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Code = Code, Message = message };
    }
}
