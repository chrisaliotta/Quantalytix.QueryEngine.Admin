using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>Builds the report rows the report and release tests seed: versions of report 60 over report dimension 500.</summary>
internal static class ReportRows
{
    /// <summary>The report version the rows belong to.</summary>
    public const long ReportVersionId = 61;

    /// <summary>A version of report 60 that reads model 40.</summary>
    public static ReportVersionRecord ReportVersion(long reportVersionId, string status = PublicationStatuses.Draft)
    {
        return new ReportVersionRecord
        {
            ReportVersionId = reportVersionId,
            ReportId = 60,
            VersionNumber = (int)(reportVersionId % 10),
            ReportDimensionId = 500,
            ModelId = ModelRows.ModelId,
            Status = status
        };
    }

    /// <summary>A visible column that returns a variable or calculated measure by key, as the value kind says.</summary>
    public static ReportColumnRecord Column(long columnId, string columnKey, string valueKind, string metricKey, int sortOrder)
    {
        return new ReportColumnRecord
        {
            ReportColumnId = columnId,
            ReportVersionId = ReportVersionId,
            ColumnKey = columnKey,
            DisplayName = columnKey,
            ValueKind = valueKind,
            MetricKey = metricKey,
            SortOrder = sortOrder,
            IsVisible = true
        };
    }

    /// <summary>A visible line; set the kind's references with <c>with</c>.</summary>
    public static ReportLineRecord Line(long lineId, string lineKey, string lineKind, long? parentLineId, int sortOrder)
    {
        return new ReportLineRecord
        {
            ReportLineId = lineId,
            ReportVersionId = ReportVersionId,
            LineKey = lineKey,
            DisplayName = lineKey,
            LineKind = lineKind,
            ParentReportLineId = parentLineId,
            SortOrder = sortOrder,
            IsVisible = true
        };
    }
}
