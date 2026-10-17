using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The calculation dimension routes of model versions: the field that produces each dimension in each dataset or source.</summary>
public sealed class CalculationRouteService(ITableGateway<CalculationRouteRecord> table, IIdGenerator ids)
    : ModelVersionRows<CalculationRouteRecord, CalculationRouteInput>(table, ids)
{
    /// <summary>Deletes a route; nothing else references routes.</summary>
    /// <param name="itemId">The route.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>Null: a route is always deleted.</returns>
    public override async Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken)
    {
        await Rows.DeleteAsync(new { CalculationBindingId = itemId }, cancellationToken);
        return null;
    }

    /// <summary>The gateway key of a route.</summary>
    /// <param name="itemId">The route ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long itemId)
    {
        return new { CalculationBindingId = itemId };
    }

    /// <summary>The model version a route belongs to.</summary>
    /// <param name="row">The route.</param>
    /// <returns>Its model version ID.</returns>
    protected override long VersionOf(CalculationRouteRecord row)
    {
        return row.ModelVersionId;
    }

    /// <summary>The key a route is listed by: its field, since routes have no key of their own.</summary>
    /// <param name="row">The route.</param>
    /// <returns>Its field key.</returns>
    protected override string KeyOf(CalculationRouteRecord row)
    {
        return row.FieldKey;
    }

    /// <summary>The route row an input describes.</summary>
    /// <param name="itemId">The route ID.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override CalculationRouteRecord ToRecord(long itemId, long versionId, CalculationRouteInput input)
    {
        return new CalculationRouteRecord
        {
            CalculationBindingId = itemId,
            ModelVersionId = versionId,
            CalculationDimensionId = input.CalculationDimensionId,
            DatasetId = input.DatasetId,
            VariableSourceId = input.VariableSourceId,
            RouteKind = input.RouteKind,
            FieldKey = input.FieldKey
        };
    }
}
