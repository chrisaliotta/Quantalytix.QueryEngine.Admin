using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.CalculationModels;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Builds the report rows the report and snapshot tests seed: enabled report 60 over report dimension 500 and model 40, and its
/// content unless changed with <c>with</c>.
/// </summary>
internal static class ReportRows
{
    /// <summary>The report the rows belong to.</summary>
    public const long ReportId = 60;

    /// <summary>The report's live version, which the content rows key by.</summary>
    public const long ReportVersionId = 61;

    /// <summary>The report's dimension.</summary>
    public const long ReportDimensionId = 500;

    /// <summary>The balance sheet report over dimension 500.</summary>
    public static ReportRecord Report()
    {
        return new ReportRecord { ReportId = ReportId, ReportKey = "balance_sheet", ReportDimensionId = ReportDimensionId };
    }

    /// <summary>The report's enabled live version 61, which reads model 40 with totals above their groups.</summary>
    public static ReportVersionRecord Version()
    {
        return new ReportVersionRecord
        {
            ReportVersionId = ReportVersionId,
            ReportId = ReportId,
            SnapshotId = SnapshotRows.LiveId,
            DisplayName = "Balance sheet",
            ReportType = "financial",
            CalculationModelId = ModelRows.CalculationModelId,
            DefaultPresentation = GroupPresentations.TotalAbove,
            IsEnabled = true,
            CreatedAt = DateTime.UnixEpoch
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
        return new ReportMemberOptionRecord { ReportVersionId = ReportVersionId, MemberKey = memberKey, IsVisible = true, ShowsValues = false, ReverseSign = false };
    }

    /// <summary>A scope that reads a variable at a member and its descendants.</summary>
    public static ReportVariableScopeRecord Scope(string variableKey, string scopeMemberKey)
    {
        return new ReportVariableScopeRecord
        {
            ReportVersionId = ReportVersionId, VariableKey = variableKey, ScopeMemberKey = scopeMemberKey, SelectionScope = SelectionScopes.Descendants
        };
    }

    /// <summary>A direct source that reads by <c>as_of_date</c>, with the row keys <c>gl_account</c> holds.</summary>
    public static ReportSourceRecord Source(long sourceId, string sourceKey, long datasetId)
    {
        return new ReportSourceRecord
        {
            ReportSourceId = sourceId,
            ReportVersionId = ReportVersionId,
            SourceKey = sourceKey,
            DisplayName = sourceKey,
            DatasetId = datasetId,
            MappingMethod = ReportMappingMethods.Direct,
            FieldKey = "gl_account",
            TimeFieldKey = "as_of_date"
        };
    }

    /// <summary>A source's supply of a variable from the <c>balance</c> measure, unfiltered.</summary>
    public static ReportSourceVariableRecord SourceVariable(long sourceId, string variableKey, string measureKey = "balance")
    {
        return new ReportSourceVariableRecord { ReportSourceId = sourceId, VariableKey = variableKey, MeasureKey = measureKey };
    }

    /// <summary>An enabled source profile.</summary>
    public static SourceProfileRecord Profile(long profileId, string profileKey)
    {
        return new SourceProfileRecord { CalculationSourceProfileId = profileId, ReportVersionId = ReportVersionId, ProfileKey = profileKey, DisplayName = profileKey, IsEnabled = true };
    }

    /// <summary>A profile's pick of a source for a variable.</summary>
    public static SourceProfileVariableRecord Pick(long profileId, string variableKey, long sourceId)
    {
        return new SourceProfileVariableRecord { CalculationSourceProfileId = profileId, VariableKey = variableKey, ReportSourceId = sourceId };
    }

    /// <summary>An enabled, groupable and filterable calculation dimension that groups a role's members by member.</summary>
    public static CalculationDimensionRecord CalculationDimension(long calculationDimensionId, string dimensionKey, long dimensionRoleId)
    {
        return new CalculationDimensionRecord
        {
            CalculationDimensionId = calculationDimensionId,
            ReportVersionId = ReportVersionId,
            DimensionKey = dimensionKey,
            DisplayName = dimensionKey,
            DimensionRoleId = dimensionRoleId,
            GroupingMode = GroupingModes.Member,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true
        };
    }

    /// <summary>A scenario of the report on a role.</summary>
    public static ReportScenarioRecord Scenario(long dimensionRoleId)
    {
        return new ReportScenarioRecord { ReportVersionId = ReportVersionId, DimensionRoleId = dimensionRoleId };
    }
}
