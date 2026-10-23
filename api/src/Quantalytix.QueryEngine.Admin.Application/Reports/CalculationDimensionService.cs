using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The calculation dimensions of reports: the shared dimensions runs break out and filter by, each a role grouped as it says,
/// which every source reads through its dataset's binding of the role.
/// </summary>
public sealed class CalculationDimensionService(ITableGateway<CalculationDimensionRecord> dimensions, IIdGenerator ids)
    : OwnedRows<CalculationDimensionRecord, CalculationDimensionInput>(dimensions, ids), IReportContent<CalculationDimensionRecord, CalculationDimensionInput>
{
    /// <summary>Deletes a calculation dimension; the report's defaults name calculation dimensions by key, which the report rules check.</summary>
    /// <param name="itemId">The calculation dimension.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>Null: a calculation dimension is always deleted.</returns>
    public override async Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken)
    {
        await Rows.DeleteAsync(Key(itemId), cancellationToken);
        return null;
    }

    /// <summary>The report with a calculation dimension added or replaced.</summary>
    /// <param name="report">The report as it stands.</param>
    /// <param name="item">The composed calculation dimension.</param>
    /// <returns>The report as the write leaves it.</returns>
    public ReportGraph Put(ReportGraph report, CalculationDimensionRecord item)
    {
        return report with { Dimensions = [.. report.Dimensions.Where(dimension => dimension.CalculationDimensionId != item.CalculationDimensionId), item] };
    }

    /// <summary>The report without a calculation dimension.</summary>
    /// <param name="report">The report as it stands.</param>
    /// <param name="itemId">The calculation dimension.</param>
    /// <returns>The report as the delete leaves it.</returns>
    public ReportGraph Without(ReportGraph report, long itemId)
    {
        return report with { Dimensions = [.. report.Dimensions.Where(dimension => dimension.CalculationDimensionId != itemId)] };
    }

    /// <summary>The gateway filter of a report's calculation dimensions.</summary>
    /// <param name="ownerId">The report.</param>
    /// <returns>The filter object.</returns>
    protected override object Owner(long ownerId)
    {
        return new { ReportId = ownerId };
    }

    /// <summary>The gateway key of a calculation dimension.</summary>
    /// <param name="itemId">The calculation dimension ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long itemId)
    {
        return new { CalculationDimensionId = itemId };
    }

    /// <summary>The report a calculation dimension belongs to.</summary>
    /// <param name="row">The calculation dimension.</param>
    /// <returns>Its report ID.</returns>
    protected override long OwnerOf(CalculationDimensionRecord row)
    {
        return row.ReportId;
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
    /// <param name="ownerId">The report.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override CalculationDimensionRecord ToRecord(long itemId, long ownerId, CalculationDimensionInput input)
    {
        return new CalculationDimensionRecord
        {
            CalculationDimensionId = itemId,
            ReportId = ownerId,
            DimensionKey = input.DimensionKey,
            DisplayName = input.DisplayName,
            RoleId = input.RoleId,
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
