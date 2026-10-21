using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Applies a write to a model's variables or calculated measures. The engine reads every write at once, so the write is first applied
/// to the model in memory and checked: the model's formulas must still resolve and stay acyclic, and every enabled report of the
/// model must still find the metrics it names and, under each enabled profile, a source for each variable it reads. A write that
/// breaks a rule writes nothing.
/// </summary>
/// <param name="variables">Reads the model's variables.</param>
/// <param name="calculatedMeasures">Reads the model's calculated measures.</param>
/// <param name="reports">Reads the reports of the model.</param>
/// <param name="reader">Reads each enabled report as it stands, and the scope it is checked in.</param>
public sealed class ModelChanges(
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<ReportRecord> reports,
    ReportGraphReader reader)
{
    /// <summary>The report rules that read a model's metrics.</summary>
    private static readonly IReportRule[] MetricRules = [new ReportMetricsResolveRule(), new ProfilesCompleteRule()];

    /// <summary>Checks a write against the model's formulas and the enabled reports that read the model, and makes it when it passes.</summary>
    /// <param name="modelId">The model written to.</param>
    /// <param name="change">The write's effect on the model's metrics.</param>
    /// <param name="write">Makes the write.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The violations the write would cause, in which case nothing was written; none when it was made.</returns>
    public async Task<IReadOnlyList<RuleViolation>> ApplyAsync(
        long modelId,
        Func<ModelGraph, ModelGraph> change,
        Func<Task> write,
        CancellationToken cancellationToken)
    {
        var owner = new { ModelId = modelId };
        var model = change(new ModelGraph
        {
            Variables = await variables.ListAsync(owner, cancellationToken),
            CalculatedMeasures = await calculatedMeasures.ListAsync(owner, cancellationToken)
        });
        List<RuleViolation> violations = [.. FormulaReferencesResolveRule.Evaluate(model), .. FormulasAcyclicRule.Evaluate(model)];
        var graphs = new List<ReportGraph>();
        foreach (var report in (await reports.ListAsync(owner, cancellationToken)).Where(report => report.IsEnabled).OrderBy(report => report.ReportKey, StringComparer.Ordinal))
            graphs.Add(await reader.ReadAsync(report, cancellationToken) with { Variables = model.Variables, CalculatedMeasures = model.CalculatedMeasures });
        if (graphs.Count > 0)
        {
            var scope = await reader.ScopeAsync(graphs, null, cancellationToken);
            violations.AddRange(graphs.SelectMany(graph => MetricRules.SelectMany(rule => rule.Evaluate(graph, scope))));
        }
        if (violations.Count > 0) return violations;

        await write();
        return [];
    }
}
