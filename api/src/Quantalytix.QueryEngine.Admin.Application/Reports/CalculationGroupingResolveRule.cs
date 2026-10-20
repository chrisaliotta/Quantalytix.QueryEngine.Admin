using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>calculation_grouping_resolve</c>: the grouping level and ancestor member keys of a report's calculation dimensions name a level or
/// member of the published version of their dimension, which the active assumption set pins and the engine resolves the keys in. A
/// key the version lacks would group nothing.
/// </summary>
/// <param name="reader">Reads the report version.</param>
/// <param name="dimensions">Reads each dimension's published version.</param>
public sealed class CalculationGroupingResolveRule(ReportGraphReader reader, PublishedDimensionReader dimensions) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "calculation_grouping_resolve";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>
    /// Reports each grouping key the published version of the calculation dimension's dimension lacks, and each calculation
    /// dimension with keys whose dimension has no published version.
    /// </summary>
    /// <param name="versionId">The report version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per missing key, or one per calculation dimension whose dimension has no published version.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var report in await reader.ReportsAsync(Kind, versionId, cancellationToken))
        {
            foreach (var dimension in report.Dimensions
                .Where(dimension => dimension.GroupingLevelKey is not null || dimension.GroupingAncestorMemberKey is not null)
                .OrderBy(dimension => dimension.DimensionKey, StringComparer.Ordinal))
            {
                var subject = $"Report '{report.ReportKey}' calculation dimension '{dimension.DimensionKey}'";
                if (await dimensions.ReadAsync(dimension.DimensionId!.Value, cancellationToken) is not { } keys)
                {
                    violations.Add(Violation($"{subject} names levels or members of its dimension, which has no published version."));
                    continue;
                }

                var version = $"v{keys.Version.VersionNumber}, the published version of its dimension";
                if (dimension.GroupingLevelKey is { } level && !keys.LevelKeys.Contains(level))
                    violations.Add(Violation($"{subject} groups at level '{level}', which is not a level of {version}."));
                if (dimension.GroupingAncestorMemberKey is { } ancestor && !keys.MemberKeys.Contains(ancestor))
                    violations.Add(Violation($"{subject} groups under ancestor member '{ancestor}', which is not a member of {version}."));
            }
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
