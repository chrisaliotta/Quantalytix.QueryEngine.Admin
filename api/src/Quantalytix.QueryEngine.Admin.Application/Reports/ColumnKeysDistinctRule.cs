using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>column_keys_distinct</c>: a report version has columns, and none is named like one of its calculation dimensions, because a
/// report table lists its breakout dimensions and its columns side by side under their keys.
/// </summary>
/// <param name="reader">Reads the report version.</param>
public sealed class ColumnKeysDistinctRule(ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "column_keys_distinct";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Reports a report version without columns, and each column named like a calculation dimension.</summary>
    /// <param name="versionId">The report version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var report in await reader.ReportsAsync(Kind, versionId, cancellationToken))
        {
            if (report.Columns.Count == 0) violations.Add(Violation($"Report '{report.ReportKey}' has no columns, so it returns nothing."));
            var dimensionKeys = report.Dimensions.Select(dimension => dimension.DimensionKey).ToHashSet(StringComparer.Ordinal);
            violations.AddRange(report.Columns
                .Where(column => dimensionKeys.Contains(column.ColumnKey))
                .Select(column => Violation($"Report '{report.ReportKey}' column '{column.ColumnKey}' is named like a calculation dimension of the report version.")));
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
