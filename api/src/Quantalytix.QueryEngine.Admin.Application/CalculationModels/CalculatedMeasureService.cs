using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The calculated measures of calculation model versions: formulas over variables and other calculated measures.</summary>
public sealed class CalculatedMeasureService(
    ITableGateway<CalculatedMeasureRecord> table,
    ReportUses reports,
    IIdGenerator ids)
    : ModelVersionRows<CalculatedMeasureRecord, CalculatedMeasureInput>(table, ids)
{
    /// <summary>Deletes a calculated measure, unless a live report column returns it or a live formula line evaluates it.</summary>
    /// <param name="itemId">The calculated measure.</param>
    /// <param name="cancellationToken">Cancels the reads and the delete.</param>
    /// <returns>Null when the measure was deleted; otherwise the detail naming the report columns and lines.</returns>
    public override async Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken)
    {
        var measure = await Rows.GetAsync(new { CalculatedMeasureId = itemId }, cancellationToken);
        var blockers = new DeleteBlockers($"Calculated measure '{measure.MeasureKey}'");
        blockers.AddNamed("used by reports", await reports.MeasureUsesAsync(measure, cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;
        await Rows.DeleteAsync(new { CalculatedMeasureId = itemId }, cancellationToken);
        return null;
    }

    /// <summary>The gateway key of a calculated measure.</summary>
    /// <param name="itemId">The calculated measure ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long itemId)
    {
        return new { CalculatedMeasureId = itemId };
    }

    /// <summary>The model version a calculated measure belongs to.</summary>
    /// <param name="row">The calculated measure.</param>
    /// <returns>Its model version ID.</returns>
    protected override long VersionOf(CalculatedMeasureRecord row)
    {
        return row.ModelVersionId;
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
    /// <param name="versionId">The model version.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override CalculatedMeasureRecord ToRecord(long itemId, long versionId, CalculatedMeasureInput input)
    {
        return new CalculatedMeasureRecord
        {
            CalculatedMeasureId = itemId,
            ModelVersionId = versionId,
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
