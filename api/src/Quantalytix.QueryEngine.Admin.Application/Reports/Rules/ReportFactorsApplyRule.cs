using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_factors_apply</c>: a bridge source whose rules add into a row with a factor other than 1, such as 0.5 to take half of
/// an item or -1 to take an item back out of a folder's row, supplies only variables whose measures apply bridge factors
/// (<c>bridge_factor_behavior</c> <c>value</c>). The engine multiplies a source row by its bridge factor only for such measures, so
/// the factor would otherwise change nothing the report shows.
/// </summary>
public sealed class ReportFactorsApplyRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_factors_apply";

    /// <summary>Reports every variable of a factored source whose measure ignores bridge factors in the release.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Holds the release's dataset versions.</param>
    /// <returns>One violation per such variable.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var factored = report.SourceItems.Where(item => item.Factor != 1m).Select(item => item.ReportSourceId).ToHashSet();
        var variablesOf = report.SourceVariables.ToLookup(variable => variable.ReportSourceId);
        foreach (var source in report.Sources
            .Where(source => factored.Contains(source.ReportSourceId) && scope.Datasets.ContainsKey(source.DatasetId))
            .OrderBy(source => source.SourceKey, StringComparer.Ordinal))
        {
            var dataset = scope.Datasets[source.DatasetId];
            foreach (var variable in variablesOf[source.ReportSourceId]
                .Where(variable => dataset.Measures.GetValueOrDefault(variable.MeasureKey) is { BridgeFactorBehavior: BridgeFactorBehaviors.Ignore })
                .OrderBy(variable => variable.VariableKey, StringComparer.Ordinal))
                yield return new RuleViolation
                {
                    Code = Code,
                    Message = $"Report '{report.ReportKey}' source '{source.SourceKey}' adds items into rows with factors other than 1, but variable " +
                        $"'{variable.VariableKey}' reads '{variable.MeasureKey}' of '{dataset.DatasetKey}', which ignores bridge factors.",
                    Target = ReportAreas.Target(ReportAreas.Sources, source.SourceKey)
                };
        }
    }
}
