using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>calculation_grouping_resolve</c>: the level or ancestor member each enabled calculation dimension groups by default exists in
/// the published version of its role's dimension, which every run reads, because a request that takes the default grouping would
/// otherwise fail.
/// </summary>
public sealed class CalculationGroupingResolveRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "calculation_grouping_resolve";

    /// <summary>
    /// Reports each grouping key the published version of the calculation dimension's template lacks, and each calculation
    /// dimension with keys whose template has no published version.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Holds the published versions of the calculation dimensions' templates.</param>
    /// <returns>One violation per missing key, or one per calculation dimension whose template has no published version.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var grouping = report.EnabledDimensions
            .Where(dimension => dimension.GroupingLevelKey is not null || dimension.GroupingAncestorMemberKey is not null)
            .OrderBy(dimension => dimension.DimensionKey, StringComparer.Ordinal);
        foreach (var dimension in grouping)
        {
            var subject = $"Report '{report.ReportKey}' calculation dimension '{dimension.DimensionKey}'";
            if (scope.Templates[dimension.DimensionRoleId] is not { } keys)
            {
                yield return Violation($"{subject} names levels or members of its role's dimension, which has no published version.");
                continue;
            }

            var version = $"v{keys.Version.VersionNumber}, the published version of its role's dimension";
            if (dimension.GroupingLevelKey is { } level && !keys.LevelKeys.Contains(level))
                yield return Violation($"{subject} groups at level '{level}', which is not a level of {version}.");
            if (dimension.GroupingAncestorMemberKey is { } ancestor && !keys.MemberKeys.Contains(ancestor))
                yield return Violation($"{subject} groups under ancestor member '{ancestor}', which is not a member of {version}.");
        }
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Code = Code, Message = message };
    }
}
