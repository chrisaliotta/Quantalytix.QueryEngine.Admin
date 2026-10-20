using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The calculation dimensions of report versions: the shared dimensions runs break out and filter by.</summary>
public sealed class CalculationDimensionService(
    ITableGateway<CalculationDimensionRecord> dimensions,
    ReportContentRemover remover,
    IIdGenerator ids)
    : ReportVersionRows<CalculationDimensionRecord, CalculationDimensionInput>(dimensions, ids)
{
    /// <summary>Deletes a calculation dimension with its routes; the version's defaults name calculation dimensions by key, which publication checks.</summary>
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

    /// <summary>The report version a calculation dimension belongs to.</summary>
    /// <param name="row">The calculation dimension.</param>
    /// <returns>Its report version ID.</returns>
    protected override long VersionOf(CalculationDimensionRecord row)
    {
        return row.ReportVersionId;
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
    /// <param name="versionId">The report version.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override CalculationDimensionRecord ToRecord(long itemId, long versionId, CalculationDimensionInput input)
    {
        return new CalculationDimensionRecord
        {
            CalculationDimensionId = itemId,
            ReportVersionId = versionId,
            DimensionKey = input.DimensionKey,
            DisplayName = input.DisplayName,
            ValueKind = input.ValueKind,
            DimensionId = input.DimensionId,
            ValueDataType = input.ValueDataType,
            GroupingMode = input.GroupingMode,
            GroupingLevelKey = input.GroupingLevelKey,
            GroupingAncestorMemberKey = input.GroupingAncestorMemberKey,
            GroupingDepth = input.GroupingDepth,
            IsGroupable = input.IsGroupable,
            IsFilterable = input.IsFilterable,
            IsEnabled = input.IsEnabled
        };
    }
}
