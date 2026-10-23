using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The variables of calculation models: the logical inputs each report fills from one of its own sources.</summary>
public sealed class VariableService(ITableGateway<VariableRecord> table, IIdGenerator ids)
    : ModelRows<VariableRecord, VariableInput>(table, ids)
{
    /// <summary>The model with a variable added or replaced.</summary>
    /// <param name="model">The model's metrics as they stand.</param>
    /// <param name="item">The composed variable.</param>
    /// <returns>The model's metrics as the write leaves them.</returns>
    public override ModelGraph Put(ModelGraph model, VariableRecord item)
    {
        return model with { Variables = [.. model.Variables.Where(variable => variable.VariableId != item.VariableId), item] };
    }

    /// <summary>The model without a variable.</summary>
    /// <param name="model">The model's metrics as they stand.</param>
    /// <param name="itemId">The variable.</param>
    /// <returns>The model's metrics as the delete leaves them.</returns>
    public override ModelGraph Without(ModelGraph model, long itemId)
    {
        return model with { Variables = [.. model.Variables.Where(variable => variable.VariableId != itemId)] };
    }

    /// <summary>The gateway key of a variable.</summary>
    /// <param name="itemId">The variable ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long itemId)
    {
        return new { VariableId = itemId };
    }

    /// <summary>The model a variable belongs to.</summary>
    /// <param name="row">The variable.</param>
    /// <returns>Its model ID.</returns>
    protected override long OwnerOf(VariableRecord row)
    {
        return row.CalculationModelId;
    }

    /// <summary>The key a variable is listed by.</summary>
    /// <param name="row">The variable.</param>
    /// <returns>Its key.</returns>
    protected override string KeyOf(VariableRecord row)
    {
        return row.VariableKey;
    }

    /// <summary>The variable row an input describes.</summary>
    /// <param name="itemId">The variable ID.</param>
    /// <param name="ownerId">The model.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override VariableRecord ToRecord(long itemId, long ownerId, VariableInput input)
    {
        return new VariableRecord
        {
            VariableId = itemId,
            CalculationModelId = ownerId,
            VariableKey = input.VariableKey,
            DisplayName = input.DisplayName,
            MissingBehavior = input.MissingBehavior,
            DisplayFormat = input.DisplayFormat,
            UnitKey = input.UnitKey
        };
    }
}
