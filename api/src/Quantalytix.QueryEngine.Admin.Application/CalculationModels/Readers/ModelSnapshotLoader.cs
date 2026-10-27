using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Reads a calculation model as stored: its content alone for the reads, and for a change set also every report over it, read once
/// through the reports module's <see cref="ReportGraphReader"/>, with the violations the stored model has.
/// </summary>
/// <param name="models">The model table.</param>
/// <param name="variables">The variable table.</param>
/// <param name="measures">The calculated measure table.</param>
/// <param name="attributions">Reads the attributions with their drivers.</param>
/// <param name="parameters">Reads the parameters.</param>
/// <param name="reports">The report table.</param>
/// <param name="reader">Reads each report as it stands, and what it is checked against.</param>
public sealed class ModelSnapshotLoader(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> measures,
    MetricAttributionService attributions,
    ModelParameterService parameters,
    ITableGateway<ReportRecord> reports,
    ReportGraphReader reader)
{
    /// <summary>Reads a model's content.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The model with its metrics by key, its attributions and its parameters.</returns>
    public async Task<ModelGraph> LoadGraphAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        var owner = new { CalculationModelId = calculationModelId };
        return new ModelGraph
        {
            Model = await models.GetAsync(owner, cancellationToken),
            Variables = [.. (await variables.ListAsync(owner, cancellationToken)).OrderBy(variable => variable.VariableKey, StringComparer.Ordinal)],
            CalculatedMeasures = [.. (await measures.ListAsync(owner, cancellationToken)).OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal)],
            Attributions = await attributions.ListAsync(calculationModelId, cancellationToken),
            Parameters = await parameters.ListAsync(calculationModelId, cancellationToken)
        };
    }

    /// <summary>The reports, enabled or not, that read a model.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The reports by key.</returns>
    public async Task<IReadOnlyList<ReportRecord>> ReadersAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        return [.. (await reports.ListAsync(new { CalculationModelId = calculationModelId }, cancellationToken)).OrderBy(report => report.ReportKey, StringComparer.Ordinal)];
    }

    /// <summary>Reads a model as a change set starts from: its content, the reports over it, and the violations it has.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<ModelSnapshot> LoadAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        var graphs = new List<ReportGraph>();
        foreach (var report in await ReadersAsync(calculationModelId, cancellationToken)) graphs.Add(await reader.ReadAsync(report, cancellationToken));
        var stored = new ModelSnapshot
        {
            Content = await LoadGraphAsync(calculationModelId, cancellationToken),
            Surroundings = new ModelSurroundings
            {
                Reports = graphs,
                Scope = graphs.Count > 0 ? await reader.ScopeAsync(graphs, null, cancellationToken) : null,
                Baseline = new HashSet<RuleViolation>()
            }
        };
        return stored with { Surroundings = stored.Surroundings with { Baseline = ModelChecks.Violations(stored).ToHashSet() } };
    }
}
