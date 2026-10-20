using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>profiles_complete</c>: every enabled profile of a report picks an enabled source for every variable its columns and formula
/// rows read, through the model version's formulas, because a run under the profile reads each variable it needs from the profile's
/// pick; a missing or disabled pick leaves the variable without data. Variables the report never reads need no pick. Report versions
/// and calculation model versions both run the rule.
/// </summary>
/// <param name="kind">The kind the rule is registered for.</param>
/// <param name="reader">Reads the reports with the model version they are checked against.</param>
public sealed class ProfilesCompleteRule(string kind, ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "profiles_complete";

    /// <summary>The kind the rule checks: report versions or calculation model versions.</summary>
    public string Kind { get; } = kind;

    /// <summary>Reports each enabled profile's missing or disabled picks.</summary>
    /// <param name="versionId">The report or model version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per variable a profile cannot fill.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var report in await reader.ReportsAsync(Kind, versionId, cancellationToken))
        {
            var graph = new FormulaGraph(report.Variables, report.CalculatedMeasures);
            var read = report.Columns.Select(column => column.MetricKey)
                .Concat(report.Lines.Select(line => line.CalculatedMeasureKey).OfType<string>())
                .SelectMany(graph.Variables)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var enabledSources = report.Sources.Select(source => source.VariableSourceId).ToHashSet();
            var picked = report.Picks.ToDictionary(pick => (pick.SourceProfileId, pick.VariableKey), pick => pick.VariableSourceId);
            foreach (var profile in report.Profiles.Where(profile => profile.IsEnabled).OrderBy(profile => profile.ProfileKey, StringComparer.Ordinal))
            {
                var subject = $"Report '{report.ReportKey}' profile '{profile.ProfileKey}'";
                foreach (var variable in read)
                {
                    if (!picked.TryGetValue((profile.SourceProfileId, variable), out var sourceId))
                        violations.Add(Violation($"{subject} picks no source for variable '{variable}'."));
                    else if (!enabledSources.Contains(sourceId))
                        violations.Add(Violation($"{subject} picks a disabled source for variable '{variable}'."));
                }
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
