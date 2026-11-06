using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The model manager's commands, which run on stored state, never inside a change set: create a model with a disabled, empty live
/// version, and delete a model that no live report reads and no frozen snapshot holds (C8). Both advance the semantic revision.
/// </summary>
/// <param name="models">The model identities.</param>
/// <param name="versions">The model versions.</param>
/// <param name="remover">Deletes the live version and the identity.</param>
/// <param name="loader">Reads the model's live version and readers.</param>
/// <param name="live">The live snapshot.</param>
/// <param name="holders">Names the frozen snapshots that hold the model.</param>
/// <param name="semanticRevision">The tenant's semantic revision.</param>
/// <param name="ids">Allocates the new rows' ids.</param>
/// <param name="time">Stamps the new version.</param>
public sealed class ModelCommands(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> versions,
    CalculationModelVersionRemover remover,
    ModelSnapshotLoader loader,
    LiveSnapshot live,
    SnapshotHolders holders,
    SemanticRevision semanticRevision,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>Creates a model with a disabled live version without metrics.</summary>
    /// <param name="input">The new model, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and the writes.</param>
    /// <returns>The model's settings, or a conflict when the key is taken.</returns>
    public async Task<Outcome<CalcModel>> CreateAsync(CalcModelCreateInput input, CancellationToken cancellationToken)
    {
        if ((await models.ListAsync(new { ModelKey = input.Key }, cancellationToken)).Count > 0)
            return Outcome<CalcModel>.Refused(Refusal.Conflict("Key taken", $"A calculation model with key '{input.Key}' already exists."));

        var model = new CalculationModelRecord { CalculationModelId = ids.NextId(), ModelKey = input.Key };
        var liveSnapshotId = await live.IdAsync(cancellationToken);
        var version = new CalculationModelVersionRecord
        {
            CalculationModelVersionId = ids.NextId(),
            CalculationModelId = model.CalculationModelId,
            SnapshotId = liveSnapshotId,
            DisplayName = input.DisplayName,
            CalendarKey = ModelCalendars.Gregorian,
            CurrencyCode = input.CurrencyCode,
            FormulaLanguageVersion = input.FormulaLanguage,
            IsEnabled = false,
            AttributionMethod = input.AttributionMethod,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await models.InsertAsync(model, cancellationToken);
        await versions.InsertAsync(version, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<CalcModel>.Success(ModelReadModels.Settings(model, version, liveSnapshotId));
    }

    /// <summary>Deletes a model nothing blocks: its live version's content first, the identity last.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when deleted; otherwise the blocked delete, with nothing deleted.</returns>
    public async Task<Refusal?> DeleteAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        var model = await models.GetAsync(new { CalculationModelId = calculationModelId }, cancellationToken);
        var version = await loader.LiveVersionAsync(calculationModelId, cancellationToken);
        var all = await versions.ListAsync(new { CalculationModelId = calculationModelId }, cancellationToken);
        var check = ModelDeleteRules.Model(
            model,
            await loader.ReadersAsync(version, cancellationToken),
            await holders.FrozenKeysAsync(all.Select(row => row.SnapshotId), cancellationToken));
        if (!check.Allowed) return Refusal.DeleteBlocked(check);

        await remover.RemoveVersionsAsync([version.CalculationModelVersionId], cancellationToken);
        await remover.RemoveIdentitiesAsync([calculationModelId], cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }
}
