using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Builds the report rows the report and release tests seed: enabled report 60 over report dimension 500 and model 40, and its
/// content unless changed with <c>with</c>.
/// </summary>
internal static class ReportRows
{
    /// <summary>The report the rows belong to.</summary>
    public const long ReportId = 60;

    /// <summary>The report's dimension.</summary>
    public const long ReportDimensionId = 500;

    /// <summary>The enabled balance sheet report over dimension 500, which reads model 40 with subtotals above.</summary>
    public static ReportRecord Report()
    {
        return new ReportRecord
        {
            ReportId = ReportId,
            ReportKey = "balance_sheet",
            DisplayName = "Balance sheet",
            ReportType = "financial",
            ReportDimensionId = ReportDimensionId,
            ModelId = ModelRows.ModelId,
            SubtotalPosition = SubtotalPositions.Above,
            IsEnabled = true
        };
    }

    /// <summary>A visible column that returns a variable or calculated measure by key, as the value kind says, under its own label.</summary>
    public static ReportColumnRecord Column(long columnId, string columnKey, string valueKind, string metricKey, int sortOrder)
    {
        return new ReportColumnRecord
        {
            ReportColumnId = columnId,
            ReportId = ReportId,
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
            ReportId = ReportId,
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
        return new ReportMemberOptionRecord { ReportId = ReportId, MemberKey = memberKey, IsVisible = true, ShowsValues = false };
    }

    /// <summary>A scope that reads a variable at a member and its descendants.</summary>
    public static ReportVariableScopeRecord Scope(string variableKey, string scopeMemberKey)
    {
        return new ReportVariableScopeRecord
        {
            ReportId = ReportId, VariableKey = variableKey, ScopeMemberKey = scopeMemberKey, SelectionScope = SelectionScopes.Descendants
        };
    }

    /// <summary>A direct source that reads by <c>as_of_date</c>, with the row keys <c>gl_account</c> holds.</summary>
    public static ReportSourceRecord Source(long sourceId, string sourceKey, long datasetId)
    {
        return new ReportSourceRecord
        {
            ReportSourceId = sourceId,
            ReportId = ReportId,
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
        return new SourceProfileRecord { SourceProfileId = profileId, ReportId = ReportId, ProfileKey = profileKey, DisplayName = profileKey, IsEnabled = true };
    }

    /// <summary>A profile's pick of a source for a variable.</summary>
    public static SourceProfileVariableRecord Pick(long profileId, string variableKey, long sourceId)
    {
        return new SourceProfileVariableRecord { SourceProfileId = profileId, VariableKey = variableKey, ReportSourceId = sourceId };
    }

    /// <summary>An enabled, groupable and filterable calculation dimension that groups a role's members by member.</summary>
    public static CalculationDimensionRecord CalculationDimension(long calculationDimensionId, string dimensionKey, long roleId)
    {
        return new CalculationDimensionRecord
        {
            CalculationDimensionId = calculationDimensionId,
            ReportId = ReportId,
            DimensionKey = dimensionKey,
            DisplayName = dimensionKey,
            RoleId = roleId,
            GroupingMode = GroupingModes.Member,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true,
            IsScenario = false
        };
    }
}
