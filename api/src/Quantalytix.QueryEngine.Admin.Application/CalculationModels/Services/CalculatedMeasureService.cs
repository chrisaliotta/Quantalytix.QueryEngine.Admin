using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The calculated measures of calculation models: formulas over variables and other calculated measures.</summary>
public sealed class CalculatedMeasureService(ITableGateway<CalculatedMeasureRecord> table, IIdGenerator ids)
    : ModelRows<CalculatedMeasureRecord, CalculatedMeasureInput>(table, ids)
{
    /// <summary>The model with a calculated measure added or replaced.</summary>
    /// <param name="model">The model's metrics as they stand.</param>
    /// <param name="item">The composed calculated measure.</param>
    /// <returns>The model's metrics as the write leaves them.</returns>
    public override ModelGraph Put(ModelGraph model, CalculatedMeasureRecord item)
    {
        return model with
        {
            CalculatedMeasures = [.. model.CalculatedMeasures.Where(measure => measure.CalculatedMeasureId != item.CalculatedMeasureId), item]
        };
    }

    /// <summary>The model without a calculated measure.</summary>
    /// <param name="model">The model's metrics as they stand.</param>
    /// <param name="itemId">The calculated measure.</param>
    /// <returns>The model's metrics as the delete leaves them.</returns>
    public override ModelGraph Without(ModelGraph model, long itemId)
    {
        return model with { CalculatedMeasures = [.. model.CalculatedMeasures.Where(measure => measure.CalculatedMeasureId != itemId)] };
    }

    /// <summary>The gateway key of a calculated measure.</summary>
    /// <param name="itemId">The calculated measure ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long itemId)
    {
        return new { CalculatedMeasureId = itemId };
    }

    /// <summary>The model a calculated measure belongs to.</summary>
    /// <param name="row">The calculated measure.</param>
    /// <returns>Its model ID.</returns>
    protected override long OwnerOf(CalculatedMeasureRecord row)
    {
        return row.CalculationModelId;
    }

    /// <summary>The key a calculated measure is listed by.</summary>
    /// <param name="row">The calculated measure.</param>
    /// <returns>Its key.</returns>
    protected override string KeyOf(CalculatedMeasureRecord row)
    {
        return row.MeasureKey;
    }

    /// <summary>The calculated measure row an input describes.</summary>
    /// <param name="itemId">The calculated measure ID.</param>
    /// <param name="ownerId">The model.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override CalculatedMeasureRecord ToRecord(long itemId, long ownerId, CalculatedMeasureInput input)
    {
        return new CalculatedMeasureRecord
        {
            CalculatedMeasureId = itemId,
            CalculationModelId = ownerId,
            MeasureKey = input.MeasureKey,
            DisplayName = input.DisplayName,
            ExpressionText = input.ExpressionText,
            OutputDataType = input.OutputDataType,
            OutputPrecision = input.OutputPrecision,
            OutputScale = input.OutputScale,
            RoundingMode = input.RoundingMode,
            UnitKey = input.UnitKey,
            DisplayFormat = input.DisplayFormat
        };
    }
}
