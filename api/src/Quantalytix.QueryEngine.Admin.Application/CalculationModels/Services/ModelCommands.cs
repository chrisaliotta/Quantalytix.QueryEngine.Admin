using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The model manager's lifecycle commands, which run on stored state, never inside a change set: create a disabled, empty model,
/// and delete a model no report reads (C8). Neither changes what the engine reads: no report reads a new or deletable model.
/// </summary>
/// <param name="models">The model table.</param>
/// <param name="variables">The variable table.</param>
/// <param name="measures">The calculated measure table.</param>
/// <param name="parameters">The parameter table.</param>
/// <param name="attributions">The attribution table.</param>
/// <param name="drivers">The attribution driver table.</param>
/// <param name="loader">Reads the model's readers.</param>
/// <param name="ids">Allocates the new model's id.</param>
public sealed class ModelCommands(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> measures,
    ITableGateway<ModelParameterRecord> parameters,
    ITableGateway<MetricAttributionRecord> attributions,
    ITableGateway<MetricAttributionDriverRecord> drivers,
    ModelSnapshotLoader loader,
    IIdGenerator ids)
{
    /// <summary>Creates a disabled model without metrics.</summary>
    /// <param name="input">The new model, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The model's settings, or a conflict when the key is taken.</returns>
    public async Task<Outcome<CalcModel>> CreateAsync(CalcModelCreateInput input, CancellationToken cancellationToken)
    {
        if ((await models.ListAsync(new { ModelKey = input.Key }, cancellationToken)).Count > 0)
            return Outcome<CalcModel>.Refused(Refusal.Conflict("Key taken", $"A calculation model with key '{input.Key}' already exists."));

        var model = new CalculationModelRecord
        {
            CalculationModelId = ids.NextId(),
            ModelKey = input.Key,
            DisplayName = input.DisplayName,
            CalendarKey = ModelCalendars.Gregorian,
            CurrencyCode = input.CurrencyCode,
            FormulaLanguageVersion = input.FormulaLanguage,
            IsEnabled = false,
            AttributionMethod = input.AttributionMethod
        };
        await models.InsertAsync(model, cancellationToken);
        return Outcome<CalcModel>.Success(ModelReadModels.Settings(model));
    }

    /// <summary>Deletes a model no report reads, its content first and the model row last.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when deleted; otherwise the blocked delete, with nothing deleted.</returns>
    public async Task<Refusal?> DeleteAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        var owner = new { CalculationModelId = calculationModelId };
        var check = ModelDeleteRules.Model(await models.GetAsync(owner, cancellationToken), await loader.ReadersAsync(calculationModelId, cancellationToken));
        if (!check.Allowed) return Refusal.DeleteBlocked(check);

        await drivers.DeleteAsync(owner, cancellationToken);
        await attributions.DeleteAsync(owner, cancellationToken);
        await variables.DeleteAsync(owner, cancellationToken);
        await measures.DeleteAsync(owner, cancellationToken);
        await parameters.DeleteAsync(owner, cancellationToken);
        await models.DeleteAsync(owner, cancellationToken);
        return null;
    }
}
