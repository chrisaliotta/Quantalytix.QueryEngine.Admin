using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The model manager's reads: the list, a model's document, a formula check against the saved model, and the delete check.</summary>
/// <param name="models">The model table.</param>
/// <param name="variables">The variable table.</param>
/// <param name="measures">The calculated measure table.</param>
/// <param name="reports">The report table.</param>
/// <param name="loader">Reads a model's content and readers.</param>
public sealed class ModelQueries(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> measures,
    ITableGateway<ReportRecord> reports,
    ModelSnapshotLoader loader)
{
    /// <summary>Lists every model of the tenant with its metric counts and readers.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items by key, ignoring case.</returns>
    public async Task<IReadOnlyList<ModelListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var variableCounts = (await variables.ListAsync(null, cancellationToken)).CountBy(variable => variable.CalculationModelId).ToDictionary();
        var measureCounts = (await measures.ListAsync(null, cancellationToken)).CountBy(measure => measure.CalculationModelId).ToDictionary();
        var readers = (await reports.ListAsync(null, cancellationToken)).OrderBy(report => report.ReportKey, StringComparer.Ordinal).ToLookup(report => report.CalculationModelId);
        return
        [
            .. (await models.ListAsync(null, cancellationToken))
                .OrderBy(model => model.ModelKey, StringComparer.OrdinalIgnoreCase)
                .Select(model => new ModelListItem(
                    ModelReadModels.Settings(model),
                    variableCounts.GetValueOrDefault(model.CalculationModelId),
                    measureCounts.GetValueOrDefault(model.CalculationModelId),
                    [.. readers[model.CalculationModelId].Select(ModelReadModels.Use)]))
        ];
    }

    /// <summary>Reads a model's document.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The document.</returns>
    public async Task<ModelDocument> DocumentAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        return ModelReadModels.Document(await loader.LoadGraphAsync(calculationModelId, cancellationToken), await loader.ReadersAsync(calculationModelId, cancellationToken));
    }

    /// <summary>Checks a formula against the saved model.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="request">The formula and the metric that owns it.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<ModelFormulaCheck> CheckFormulaAsync(long calculationModelId, ModelFormulaCheckRequest request, CancellationToken cancellationToken)
    {
        return ModelFormulaChecker.Check(await loader.LoadGraphAsync(calculationModelId, cancellationToken), request.Formula, request.SelfKey);
    }

    /// <summary>Whether a model can be deleted.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<DeleteCheck> DeleteCheckAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        var model = await models.GetAsync(new { CalculationModelId = calculationModelId }, cancellationToken);
        return ModelDeleteRules.Model(model, await loader.ReadersAsync(calculationModelId, cancellationToken));
    }
}
