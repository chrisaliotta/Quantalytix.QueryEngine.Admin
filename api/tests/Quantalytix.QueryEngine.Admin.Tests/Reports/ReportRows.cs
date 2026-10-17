using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>Builds the report rows the report and release tests seed: versions of report 60 over report dimension 500.</summary>
internal static class ReportRows
{
    /// <summary>The report version the rows belong to.</summary>
    public const long ReportVersionId = 61;

    /// <summary>A version of report 60 that reads a model version.</summary>
    public static ReportVersionRecord ReportVersion(long reportVersionId, long modelVersionId = ModelRows.ModelVersionId, string status = PublicationStatuses.Draft)
    {
        return new ReportVersionRecord
        {
            ReportVersionId = reportVersionId,
            ReportId = 60,
            VersionNumber = (int)(reportVersionId % 10),
            ReportDimensionId = 500,
            ModelVersionId = modelVersionId,
            Status = status
        };
    }

    /// <summary>A visible column that returns a variable or calculated measure, as the value kind says.</summary>
    public static ReportColumnRecord Column(long columnId, string columnKey, string valueKind, long metricId, int sortOrder)
    {
        var variable = valueKind == ReportValueKinds.Variable;
        return new ReportColumnRecord
        {
            ReportColumnId = columnId,
            ReportVersionId = ReportVersionId,
            ColumnKey = columnKey,
            DisplayName = columnKey,
            ValueKind = valueKind,
            VariableId = variable ? metricId : null,
            CalculatedMeasureId = variable ? null : metricId,
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
