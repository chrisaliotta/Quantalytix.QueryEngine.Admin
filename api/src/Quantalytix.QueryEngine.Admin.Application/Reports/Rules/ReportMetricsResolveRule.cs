using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_metrics_resolve</c>: every metric key the report names exists in its model: columns name variables or calculated
/// measures as their value kind says, formula rows calculated measures, and scopes, source variables and profile picks variables.
/// </summary>
public sealed class ReportMetricsResolveRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_metrics_resolve";

    /// <summary>Reports each metric key the model lacks.</summary>
    /// <param name="report">The report, with its model's metrics.</param>
    /// <param name="scope">Unused: metrics are the model's.</param>
    /// <returns>One violation per unresolved metric.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var subject = $"Report '{report.ReportKey}'";
        var variables = report.Variables.Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal);
        var measures = report.CalculatedMeasures.Select(measure => measure.MeasureKey).ToHashSet(StringComparer.Ordinal);
        var profileKeys = report.Profiles.ToDictionary(profile => profile.CalculationSourceProfileId, profile => profile.ProfileKey);
        var sourceKeys = report.Sources.ToDictionary(source => source.ReportSourceId, source => source.SourceKey);
        foreach (var column in report.Columns.Where(column => !(column.ValueKind == ReportValueKinds.Variable ? variables : measures).Contains(column.MetricKey)))
            yield return Violation($"{subject} column '{column.ColumnKey}' returns '{column.MetricKey}', which is no {column.ValueKind} metric of the model.");
        foreach (var line in report.Lines.Where(line => line.CalculatedMeasureKey is { } key && !measures.Contains(key)))
            yield return Violation($"{subject} row '{line.LineKey}' evaluates '{line.CalculatedMeasureKey}', which is no calculated measure of the model.");
        foreach (var scoped in report.Scopes.Where(scoped => !variables.Contains(scoped.VariableKey)))
            yield return Violation($"{subject} scopes '{scoped.VariableKey}', which is no variable of the model.");
        foreach (var supplied in report.SourceVariables.Where(supplied => !variables.Contains(supplied.VariableKey)))
            yield return Violation($"{subject} source '{sourceKeys[supplied.ReportSourceId]}' supplies '{supplied.VariableKey}', which is no variable of the model.");
        foreach (var pick in report.Picks.Where(pick => !variables.Contains(pick.VariableKey)))
            yield return Violation($"{subject} profile '{profileKeys[pick.CalculationSourceProfileId]}' picks a source for '{pick.VariableKey}', which is no variable of the model.");
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
