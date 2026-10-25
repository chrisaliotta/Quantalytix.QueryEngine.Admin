using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Reads, creates, edits and deletes calculation models, whose variables and calculated measures reports read.</summary>
public sealed class CalculationModelService(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<MetricAttributionRecord> attributions,
    ITableGateway<MetricAttributionDriverRecord> attributionDrivers,
    ITableGateway<ModelParameterRecord> parameters,
    ReportUses reports,
    IIdGenerator ids)
{
    /// <summary>The calendar every model follows.</summary>
    private const string Gregorian = "gregorian";

    /// <summary>Lists every calculation model.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The models by key, ignoring case.</returns>
    public async Task<IReadOnlyList<CalculationModelRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await models.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.ModelKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one calculation model.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model.</returns>
    public Task<CalculationModelRecord> GetAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        return models.GetAsync(new { CalculationModelId = calculationModelId }, cancellationToken);
    }

    /// <summary>Creates a calculation model without variables or calculated measures.</summary>
    /// <param name="input">The new model, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored model.</returns>
    public async Task<CalculationModelRecord> CreateAsync(NewCalculationModelInput input, CancellationToken cancellationToken)
    {
        var model = new CalculationModelRecord
        {
            CalculationModelId = ids.NextId(),
            ModelKey = input.ModelKey,
            DisplayName = input.DisplayName,
            CalendarKey = Gregorian,
            CurrencyCode = input.CurrencyCode,
            FormulaLanguageVersion = input.FormulaLanguageVersion,
            IsEnabled = input.IsEnabled,
            AttributionMethod = input.AttributionMethod
        };
        await models.InsertAsync(model, cancellationToken);
        return model;
    }

    /// <summary>The model as an edit of its attributes would leave it, for <see cref="ModelChanges"/> to check.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="input">The new attributes, already validated.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model to store.</returns>
    public async Task<CalculationModelRecord> ComposeAsync(long calculationModelId, CalculationModelInput input, CancellationToken cancellationToken)
    {
        return await GetAsync(calculationModelId, cancellationToken) with
        {
            DisplayName = input.DisplayName,
            CurrencyCode = input.CurrencyCode,
            IsEnabled = input.IsEnabled,
            FormulaLanguageVersion = input.FormulaLanguageVersion,
            AttributionMethod = input.AttributionMethod
        };
    }

    /// <summary>Stores a model's attributes; the reports that read the model show its new currency and attribute by its new method.</summary>
    /// <param name="model">A model <see cref="ComposeAsync"/> made and the model gate let through.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the model is stored.</returns>
    public Task UpdateAsync(CalculationModelRecord model, CancellationToken cancellationToken)
    {
        return models.UpdateAsync(model, cancellationToken);
    }

    /// <summary>Deletes a model that no report reads, with its variables, calculated measures, attributions and parameters.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the model was deleted; otherwise the detail naming the reports that read it, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        var model = await GetAsync(calculationModelId, cancellationToken);
        var blockers = new DeleteBlockers($"Calculation model '{model.ModelKey}'");
        blockers.AddNamed("read by", await reports.ModelReadersAsync(calculationModelId, cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        var owner = new { CalculationModelId = calculationModelId };
        await attributionDrivers.DeleteAsync(owner, cancellationToken);
        await attributions.DeleteAsync(owner, cancellationToken);
        await variables.DeleteAsync(owner, cancellationToken);
        await calculatedMeasures.DeleteAsync(owner, cancellationToken);
        await parameters.DeleteAsync(owner, cancellationToken);
        await models.DeleteAsync(owner, cancellationToken);
        return null;
    }
}
