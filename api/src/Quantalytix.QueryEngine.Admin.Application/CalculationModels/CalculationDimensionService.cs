using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The calculation dimensions of model versions: the dimensions their datasets share, which reports break out and filter by.</summary>
public sealed class CalculationDimensionService(
    ITableGateway<CalculationDimensionRecord> dimensions,
    ModelContentRemover remover,
    IIdGenerator ids)
    : ModelVersionRows<CalculationDimensionRecord, CalculationDimensionInput>(dimensions, ids)
{
    /// <summary>Deletes a calculation dimension with its routes; reports name calculation dimensions by key, not ID.</summary>
    /// <param name="itemId">The calculation dimension.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>Null: a calculation dimension is always deleted.</returns>
    public override async Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken)
    {
        await remover.RemoveDimensionsAsync([itemId], cancellationToken);
        return null;
    }

    /// <summary>The gateway key of a calculation dimension.</summary>
    /// <param name="itemId">The calculation dimension ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long itemId)
    {
        return new { CalculationDimensionId = itemId };
    }

    /// <summary>The model version a calculation dimension belongs to.</summary>
    /// <param name="row">The calculation dimension.</param>
    /// <returns>Its model version ID.</returns>
    protected override long VersionOf(CalculationDimensionRecord row)
    {
        return row.ModelVersionId;
    }

    /// <summary>The key a calculation dimension is listed by.</summary>
    /// <param name="row">The calculation dimension.</param>
    /// <returns>Its key.</returns>
    protected override string KeyOf(CalculationDimensionRecord row)
    {
        return row.DimensionKey;
    }

    /// <summary>The calculation dimension row an input describes.</summary>
    /// <param name="itemId">The calculation dimension ID.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override CalculationDimensionRecord ToRecord(long itemId, long versionId, CalculationDimensionInput input)
    {
        return new CalculationDimensionRecord
        {
            CalculationDimensionId = itemId,
            ModelVersionId = versionId,
            DimensionKey = input.DimensionKey,
            DisplayName = input.DisplayName,
            ValueKind = input.ValueKind,
            DimensionId = input.DimensionId,
            ValueDataType = input.ValueDataType,
            IsGroupable = input.IsGroupable,
            IsFilterable = input.IsFilterable,
            IsEnabled = input.IsEnabled
        };
    }
}
