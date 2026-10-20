using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Builds the report rows the report and release tests seed: report 60 over report dimension 500, and the content of its version
/// 61 unless changed with <c>with</c>.
/// </summary>
internal static class ReportRows
{
    /// <summary>The report the rows belong to.</summary>
    public const long ReportId = 60;

    /// <summary>The report version the rows belong to.</summary>
    public const long ReportVersionId = 61;

    /// <summary>The report's dimension.</summary>
    public const long ReportDimensionId = 500;

    /// <summary>The balance sheet report over dimension 500.</summary>
    public static ReportRecord Report()
    {
        return new ReportRecord
        {
            ReportId = ReportId,
            ReportKey = "balance_sheet",
            DisplayName = "Balance sheet",
            ReportType = "financial",
            ReportDimensionId = ReportDimensionId,
            IsEnabled = true
        };
    }

    /// <summary>A version of report 60 that reads model 40, with subtotals above.</summary>
    public static ReportVersionRecord ReportVersion(long reportVersionId, string status = PublicationStatuses.Draft)
    {
        return new ReportVersionRecord
        {
            ReportVersionId = reportVersionId,
            ReportId = ReportId,
            VersionNumber = (int)(reportVersionId % 10),
            ModelId = ModelRows.ModelId,
            SubtotalPosition = SubtotalPositions.Above,
            Status = status
        };
    }

    /// <summary>A visible column that returns a variable or calculated measure by key, as the value kind says, under its own label.</summary>
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

    /// <summary>A visible overlay row; set a formula row's references with <c>with</c>.</summary>
    public static ReportLineRecord Line(long lineId, string lineKey, string lineKind, string? parentKey, int sortOrder)
    {
        return new ReportLineRecord
        {
            ReportLineId = lineId,
            ReportVersionId = ReportVersionId,
            LineKey = lineKey,
            DisplayName = lineKey,
            LineKind = lineKind,
            ParentKey = parentKey,
            SortOrder = sortOrder,
            IsVisible = true
        };
    }

    /// <summary>A member option that makes a member a header.</summary>
    public static ReportMemberOptionRecord MemberOption(string memberKey)
    {
        return new ReportMemberOptionRecord { ReportVersionId = ReportVersionId, MemberKey = memberKey, IsVisible = true, ShowsValues = false };
    }

    /// <summary>A scope that reads a variable at a member and its descendants.</summary>
    public static ReportVariableScopeRecord Scope(string variableKey, string scopeMemberKey)
    {
        return new ReportVariableScopeRecord
        {
            ReportVersionId = ReportVersionId, VariableKey = variableKey, ScopeMemberKey = scopeMemberKey, SelectionScope = SelectionScopes.Descendants
        };
    }

    /// <summary>An enabled source of a variable that reads <c>balance</c> by <c>as_of_date</c>, classifying by the member keys <c>gl_account</c> holds.</summary>
    public static VariableSourceRecord Source(long sourceId, string variableKey, string sourceKey, long datasetId)
    {
        return new VariableSourceRecord
        {
            VariableSourceId = sourceId,
            ReportVersionId = ReportVersionId,
            VariableKey = variableKey,
            SourceKey = sourceKey,
            DatasetId = datasetId,
            ClassificationKind = ClassificationKinds.Direct,
            ClassificationFieldKey = "gl_account",
            TimeFieldKey = "as_of_date",
            MeasureKey = "balance",
            IsEnabled = true
        };
    }

    /// <summary>An enabled source profile.</summary>
    public static SourceProfileRecord Profile(long profileId, string profileKey)
    {
        return new SourceProfileRecord { SourceProfileId = profileId, ReportVersionId = ReportVersionId, ProfileKey = profileKey, DisplayName = profileKey, IsEnabled = true };
    }

    /// <summary>A profile's pick of a source for a variable.</summary>
    public static SourceProfileVariableRecord Pick(long profileId, string variableKey, long sourceId)
    {
        return new SourceProfileVariableRecord { SourceProfileId = profileId, VariableKey = variableKey, VariableSourceId = sourceId };
    }

    /// <summary>An enabled, groupable and filterable calculation dimension that groups a role's members by member.</summary>
    public static CalculationDimensionRecord CalculationDimension(long calculationDimensionId, string dimensionKey, long roleId)
    {
        return new CalculationDimensionRecord
        {
            CalculationDimensionId = calculationDimensionId,
            ReportVersionId = ReportVersionId,
            DimensionKey = dimensionKey,
            DisplayName = dimensionKey,
            RoleId = roleId,
            GroupingMode = GroupingModes.Member,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true
        };
    }
}
