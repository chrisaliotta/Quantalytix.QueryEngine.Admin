using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_metrics_resolve</c>: every column returns a variable or calculated measure of the model version, by its value kind and
/// key, every formula row evaluates a calculated measure of it, and every scope, source and profile pick names a variable of it,
/// because the orchestrator reads the metrics by key. Report versions and calculation model versions both run the rule.
/// </summary>
/// <param name="kind">The kind the rule is registered for.</param>
/// <param name="reader">Reads the reports with the model version they are checked against.</param>
public sealed class ReportMetricsResolveRule(string kind, ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_metrics_resolve";

    /// <summary>The kind the rule checks: report versions or calculation model versions.</summary>
    public string Kind { get; } = kind;

    /// <summary>Reports each report row whose metric the model version lacks.</summary>
    /// <param name="versionId">The report or model version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unresolved metric.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var report in await reader.ReportsAsync(Kind, versionId, cancellationToken))
        {
            var subject = $"Report '{report.ReportKey}'";
            var variables = report.Variables.Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal);
            var measures = report.CalculatedMeasures.Select(measure => measure.MeasureKey).ToHashSet(StringComparer.Ordinal);
            var profileKeys = report.Profiles.ToDictionary(profile => profile.SourceProfileId, profile => profile.ProfileKey);
            foreach (var column in report.Columns.Where(column => !(column.ValueKind == ReportValueKinds.Variable ? variables : measures).Contains(column.MetricKey)))
                violations.Add(Violation($"{subject} column '{column.ColumnKey}' returns '{column.MetricKey}', which is no {column.ValueKind} metric of the model version."));
            foreach (var line in report.Lines.Where(line => line.CalculatedMeasureKey is { } key && !measures.Contains(key)))
                violations.Add(Violation($"{subject} row '{line.LineKey}' evaluates '{line.CalculatedMeasureKey}', which is no calculated measure of the model version."));
            foreach (var scope in report.Scopes.Where(scope => !variables.Contains(scope.VariableKey)))
                violations.Add(Violation($"{subject} scopes '{scope.VariableKey}', which is no variable of the model version."));
            foreach (var source in report.Sources.Where(source => !variables.Contains(source.VariableKey)))
                violations.Add(Violation($"{subject} source '{source.SourceKey}' supplies '{source.VariableKey}', which is no variable of the model version."));
            foreach (var pick in report.Picks.Where(pick => !variables.Contains(pick.VariableKey)))
                violations.Add(Violation($"{subject} profile '{profileKeys[pick.SourceProfileId]}' picks a source for '{pick.VariableKey}', which is no variable of the model version."));
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
