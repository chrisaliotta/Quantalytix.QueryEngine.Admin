using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Model version content stored in one table: rows owned by <c>model_version_id</c>.</summary>
/// <typeparam name="TRecord">The content row.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
/// <param name="rows">The content table.</param>
/// <param name="ids">Allocates the IDs of new rows.</param>
public abstract class ModelVersionRows<TRecord, TInput>(ITableGateway<TRecord> rows, IIdGenerator ids) : OwnedRows<TRecord, TInput>(rows, ids)
    where TRecord : class
{
    /// <summary>The gateway filter of a model version's rows.</summary>
    /// <param name="ownerId">The model version.</param>
    /// <returns>The filter object.</returns>
    protected sealed override object Owner(long ownerId)
    {
        return new { ModelVersionId = ownerId };
    }
}
