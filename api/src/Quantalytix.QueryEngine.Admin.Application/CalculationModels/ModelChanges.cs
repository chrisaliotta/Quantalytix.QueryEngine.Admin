using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Applies a write to a model, its variables, calculated measures, attributions or parameters. The engine reads every write at once,
/// so the write is first applied to the model in memory and checked: the model's formulas must still resolve, stay acyclic, fit its
/// formula language and read each parameter where its type belongs, its attributions must still resolve and partition their
/// expressions, every report of the model, enabled or not, must still set only parameters the model declares, as their types and
/// allowed values say, and every enabled report of the model must still find the metrics it names and, under each enabled profile,
/// a source for each variable it reads, and compare no parameter of the model in its default filter. A write that breaks a rule
/// writes nothing.
/// </summary>
/// <param name="models">Reads the model.</param>
/// <param name="variables">Reads the model's variables.</param>
/// <param name="calculatedMeasures">Reads the model's calculated measures.</param>
/// <param name="attributions">Reads the model's attributions.</param>
/// <param name="parameters">Reads the model's parameters.</param>
/// <param name="reports">Lists the reports of the model.</param>
/// <param name="reader">Reads each report as it stands, and the scope it is checked in.</param>
public sealed class ModelChanges(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    MetricAttributionService attributions,
    ModelParameterService parameters,
    ReportUses reports,
    ReportGraphReader reader)
{
    /// <summary>The rules every model passes on its own.</summary>
    private static readonly Func<ModelGraph, IEnumerable<RuleViolation>>[] ModelRules =
    [
        FormulaReferencesResolveRule.Evaluate,
        FormulasAcyclicRule.Evaluate,
        FormulasFitLanguageRule.Evaluate,
        FormulaParametersResolveRule.Evaluate,
        AttributionReferencesResolveRule.Evaluate,
        AttributionDriversRule.Evaluate,
        AttributionWeightVariableRule.Evaluate
    ];

    /// <summary>The report rule that reads a model's parameters, which every report's stored row values must keep passing.</summary>
    private static readonly IReportRule ParameterRule = new ReportRowParametersResolveRule();

    /// <summary>The report rules that read a model's metrics, and the names of its parameters, which no default filter may compare.</summary>
    private static readonly IReportRule[] EnabledReportRules = [new ReportMetricsResolveRule(), new ProfilesCompleteRule(), new ReportDefaultsResolveRule()];

    /// <summary>Checks a write against the model's rules and the reports that read the model, and makes it when it passes.</summary>
    /// <param name="modelId">The model written to.</param>
    /// <param name="change">The write's effect on the model.</param>
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
            Model = await models.GetAsync(owner, cancellationToken),
            Variables = await variables.ListAsync(owner, cancellationToken),
            CalculatedMeasures = await calculatedMeasures.ListAsync(owner, cancellationToken),
            Attributions = await attributions.ListAsync(modelId, cancellationToken),
            Parameters = await parameters.ListAsync(modelId, cancellationToken)
        });
        List<RuleViolation> violations = [.. ModelRules.SelectMany(rule => rule(model))];
        var graphs = new List<ReportGraph>();
        foreach (var report in await reports.OfModelAsync(modelId, cancellationToken))
        {
            var graph = await reader.ReadAsync(report, cancellationToken);
            graphs.Add(graph with { Variables = model.Variables, CalculatedMeasures = model.CalculatedMeasures, Parameters = model.Parameters });
        }
        if (graphs.Count > 0)
        {
            var scope = await reader.ScopeAsync(graphs, null, cancellationToken);
            violations.AddRange(graphs.SelectMany(graph => ParameterRule.Evaluate(graph, scope)));
            violations.AddRange(graphs.Where(graph => graph.Report.IsEnabled).SelectMany(graph => EnabledReportRules.SelectMany(rule => rule.Evaluate(graph, scope))));
        }
        if (violations.Count > 0) return violations;

        await write();
        return [];
    }
}
