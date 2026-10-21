using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Reads, creates, edits and deletes calculation models, whose variables and calculated measures reports read.</summary>
public sealed class CalculationModelService(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ReportUses reports,
    IIdGenerator ids)
{
    /// <summary>The calendar every model follows.</summary>
    private const string Gregorian = "gregorian";

    /// <summary>The grammar version of every model formula: the query engine's expression language.</summary>
    private const string FormulaLanguageVersion = "1.0";

    /// <summary>Lists every calculation model.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The models by key, ignoring case.</returns>
    public async Task<IReadOnlyList<CalculationModelRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await models.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.ModelKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one calculation model.</summary>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model.</returns>
    public Task<CalculationModelRecord> GetAsync(long modelId, CancellationToken cancellationToken)
    {
        return models.GetAsync(new { ModelId = modelId }, cancellationToken);
    }

    /// <summary>Creates a calculation model without variables or calculated measures.</summary>
    /// <param name="input">The new model, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored model.</returns>
    public async Task<CalculationModelRecord> CreateAsync(NewCalculationModelInput input, CancellationToken cancellationToken)
    {
        var model = new CalculationModelRecord
        {
            ModelId = ids.NextId(),
            ModelKey = input.ModelKey,
            DisplayName = input.DisplayName,
            CalendarKey = Gregorian,
            CurrencyCode = input.CurrencyCode,
            FormulaLanguageVersion = FormulaLanguageVersion,
            IsEnabled = input.IsEnabled
        };
        await models.InsertAsync(model, cancellationToken);
        return model;
    }

    /// <summary>Edits a model's display name, currency and availability; the reports that read the model show its new currency.</summary>
    /// <param name="modelId">The model.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored model.</returns>
    public async Task<CalculationModelRecord> UpdateAsync(long modelId, CalculationModelInput input, CancellationToken cancellationToken)
    {
        var updated = await GetAsync(modelId, cancellationToken) with
        {
            DisplayName = input.DisplayName, CurrencyCode = input.CurrencyCode, IsEnabled = input.IsEnabled
        };
        await models.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Deletes a model that no report reads, with its variables and calculated measures.</summary>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the model was deleted; otherwise the detail naming the reports that read it, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long modelId, CancellationToken cancellationToken)
    {
        var model = await GetAsync(modelId, cancellationToken);
        var blockers = new DeleteBlockers($"Calculation model '{model.ModelKey}'");
        blockers.AddNamed("read by", await reports.ModelReadersAsync(modelId, cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        var owner = new { ModelId = modelId };
        await variables.DeleteAsync(owner, cancellationToken);
        await calculatedMeasures.DeleteAsync(owner, cancellationToken);
        await models.DeleteAsync(owner, cancellationToken);
        return null;
    }
}
