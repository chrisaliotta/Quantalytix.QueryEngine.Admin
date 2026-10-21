using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>profiles_complete</c>: every enabled profile of a report picks, for every variable its columns and formula rows read through
/// the model's formulas, a source that supplies the variable, because a run under the profile reads each variable it needs
/// from the profile's pick. Variables the report never reads need no pick.
/// </summary>
public sealed class ProfilesCompleteRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "profiles_complete";

    /// <summary>Reports each enabled profile's missing picks, and picks of a source that does not supply the variable.</summary>
    /// <param name="report">The report, with its model's metrics.</param>
    /// <param name="scope">Unused: profiles and sources are the report's own.</param>
    /// <returns>One violation per variable a profile cannot fill.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var graph = new FormulaGraph(report.Variables, report.CalculatedMeasures);
        var read = report.Columns.Select(column => column.MetricKey)
            .Concat(report.Lines.Select(line => line.CalculatedMeasureKey).OfType<string>())
            .SelectMany(graph.Variables)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var supplied = report.SourceVariables.Select(variable => (variable.ReportSourceId, variable.VariableKey)).ToHashSet();
        var picked = report.Picks.ToDictionary(pick => (pick.SourceProfileId, pick.VariableKey), pick => pick.ReportSourceId);
        foreach (var profile in report.Profiles.Where(profile => profile.IsEnabled).OrderBy(profile => profile.ProfileKey, StringComparer.Ordinal))
        {
            var subject = $"Report '{report.ReportKey}' profile '{profile.ProfileKey}'";
            foreach (var variable in read)
            {
                if (!picked.TryGetValue((profile.SourceProfileId, variable), out var sourceId))
                    yield return Violation($"{subject} picks no source for variable '{variable}'.");
                else if (!supplied.Contains((sourceId, variable)))
                    yield return Violation($"{subject} picks a source that does not supply variable '{variable}'.");
            }
        }
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
