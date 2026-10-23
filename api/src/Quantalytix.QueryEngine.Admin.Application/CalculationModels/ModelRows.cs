using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Model content stored in one table: rows owned by <c>calculation_model_id</c>. <see cref="ModelChanges"/> checks every write, deletes included,
/// against the model's formulas and the enabled reports that read the model, so a delete has no blockers of its own.
/// </summary>
/// <typeparam name="TRecord">The content row.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
/// <param name="rows">The content table.</param>
/// <param name="ids">Allocates the IDs of new rows.</param>
public abstract class ModelRows<TRecord, TInput>(ITableGateway<TRecord> rows, IIdGenerator ids)
    : OwnedRows<TRecord, TInput>(rows, ids), IModelContent<TRecord, TInput>
    where TRecord : class
{
    /// <summary>Deletes a row.</summary>
    /// <param name="itemId">The row, one of the model's.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>Null: a row the model gate let through is always deleted.</returns>
    public sealed override async Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken)
    {
        await Rows.DeleteAsync(Key(itemId), cancellationToken);
        return null;
    }

    /// <summary>The model with a row added, or in place of the stored row with its ID.</summary>
    /// <param name="model">The model's metrics as they stand.</param>
    /// <param name="item">The composed row.</param>
    /// <returns>The model's metrics as the write leaves them.</returns>
    public abstract ModelGraph Put(ModelGraph model, TRecord item);

    /// <summary>The model without a row.</summary>
    /// <param name="model">The model's metrics as they stand.</param>
    /// <param name="itemId">The row.</param>
    /// <returns>The model's metrics as the delete leaves them.</returns>
    public abstract ModelGraph Without(ModelGraph model, long itemId);

    /// <summary>The gateway filter of a model's rows.</summary>
    /// <param name="ownerId">The model.</param>
    /// <returns>The filter object.</returns>
    protected sealed override object Owner(long ownerId)
    {
        return new { CalculationModelId = ownerId };
    }
}
