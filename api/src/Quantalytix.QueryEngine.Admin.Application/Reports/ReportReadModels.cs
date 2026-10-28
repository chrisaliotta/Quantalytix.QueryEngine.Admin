using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Builds the contract's read models of a report from its rows.</summary>
public static class ReportReadModels
{
    /// <summary>A report as the library lists it.</summary>
    /// <param name="report">The report row.</param>
    /// <param name="modelKey">The key of the report's model.</param>
    /// <param name="profileCount">How many profiles it has.</param>
    /// <param name="sourceCount">How many sources it has.</param>
    /// <returns>The summary.</returns>
    public static ReportSummary Summary(ReportRecord report, string modelKey, int profileCount, int sourceCount)
    {
        return new ReportSummary
        {
            Id = report.ReportId,
            Key = report.ReportKey,
            DisplayName = report.DisplayName,
            ReportType = report.ReportType,
            ModelId = report.CalculationModelId,
            ModelKey = modelKey,
            IsEnabled = report.IsEnabled,
            Periodicity = ReportSettingsColumns.Read(report).Periodicity,
            ProfileCount = profileCount,
            SourceCount = sourceCount
        };
    }

    /// <summary>The whole report.</summary>
    /// <param name="snapshot">The report, stored or pending.</param>
    /// <returns>The document.</returns>
    public static ReportDocument Document(ReportSnapshot snapshot)
    {
        var report = snapshot.Content;
        return new ReportDocument
        {
            Id = report.Report.ReportId,
            Key = report.ReportKey,
            IsEnabled = report.Report.IsEnabled,
            ModelId = report.Report.CalculationModelId,
            ReportDimensionId = report.Report.ReportDimensionId,
            ReportDimensionVersionId = snapshot.Surroundings.DimensionVersionId,
            Settings = ReportSettingsColumns.Read(report.Report),
            Rows = Rows(report),
            Sources = [.. report.Sources.OrderBy(source => source.SourceKey, StringComparer.Ordinal).Select(source => Source(report, source))],
            Profiles = [.. report.Profiles.OrderBy(profile => profile.ProfileKey, StringComparer.Ordinal).Select(profile => Profile(report, profile))],
            CalculationDimensions = [.. report.Dimensions.OrderBy(dimension => dimension.DimensionKey, StringComparer.Ordinal).Select(CalculationDimension)],
            Scenarios = [.. report.Scenarios.OrderBy(scenario => scenario.DimensionRoleId).Select(scenario => new ReportScenario { RoleId = scenario.DimensionRoleId })],
            VariableScopes = [.. report.Scopes.OrderBy(scope => scope.VariableKey, StringComparer.Ordinal).Select(Scope)],
            Columns = [.. report.Columns.OrderBy(column => column.SortOrder).Select(Column)],
            Styles = [.. report.Styles.OrderBy(style => style.StyleKey, StringComparer.Ordinal).Select(Style)]
        };
    }

    /// <summary>A grouping of a calculation dimension row.</summary>
    /// <param name="dimension">The calculation dimension.</param>
    /// <returns>The grouping.</returns>
    public static Grouping GroupingOf(CalculationDimensionRecord dimension)
    {
        return dimension.GroupingMode switch
        {
            GroupingModes.Level => new GroupingLevel { LevelKey = dimension.GroupingLevelKey! },
            GroupingModes.Ancestor => new GroupingAncestor { MemberKey = dimension.GroupingAncestorMemberKey! },
            GroupingModes.Depth => new GroupingDepth { Depth = dimension.GroupingDepth!.Value },
            _ => new GroupingMember()
        };
    }

    /// <summary>Every row in hierarchy order.</summary>
    private static IReadOnlyList<ReportRow> Rows(ReportGraph report)
    {
        var options = report.MemberOptions.ToDictionary(option => option.MemberKey, StringComparer.Ordinal);
        var columnKeys = report.Columns.ToDictionary(column => column.ReportColumnId, column => column.ColumnKey);
        var values = report.RowParameters.ToLookup(value => value.RowKey, StringComparer.Ordinal);
        return
        [
            .. ReportRowTree.InOrder(ReportRowTree.Of(report)).Select(row =>
            {
                var parameters = values[row.Key].OrderBy(value => value.ParameterKey, StringComparer.Ordinal).ToDictionary(value => value.ParameterKey, RowParameterJson.Of, StringComparer.Ordinal);
                if (row.Member is { } member)
                {
                    var option = options.GetValueOrDefault(member.MemberKey);
                    return new ReportRow
                    {
                        Key = row.Key,
                        Label = option?.DisplayName ?? member.DisplayName,
                        Kind = member.IsAssignable ? ReportRowKinds.Data : ReportRowKinds.Group,
                        ParentKey = row.ParentKey,
                        IsVisible = option?.IsVisible ?? true,
                        ShowsValues = option?.ShowsValues ?? true,
                        CalculatedMeasureKey = null,
                        ValueColumnKey = null,
                        StyleKey = option?.StyleKey,
                        Parameters = parameters
                    };
                }

                var line = row.Line!;
                return new ReportRow
                {
                    Key = row.Key,
                    Label = line.DisplayName,
                    Kind = line.LineKind,
                    ParentKey = row.ParentKey,
                    IsVisible = line.IsVisible,
                    ShowsValues = true,
                    CalculatedMeasureKey = line.CalculatedMeasureKey,
                    ValueColumnKey = line.ValueReportColumnId is { } columnId ? columnKeys[columnId] : null,
                    StyleKey = line.StyleKey,
                    Parameters = parameters
                };
            })
        ];
    }

    /// <summary>A source with its variables, rules and left-out items.</summary>
    private static ReportSource Source(ReportGraph report, ReportSourceRecord source)
    {
        var input = new ReportSourceInput
        {
            Key = source.SourceKey,
            DisplayName = source.DisplayName,
            DatasetId = source.DatasetId,
            TimeFieldKey = source.TimeFieldKey,
            Method = source.MappingMethod,
            RoleId = source.DimensionRoleId,
            FieldKey = source.FieldKey
        };
        return new ReportSource(
            input,
            source.BridgeId,
            [
                .. report.SourceVariables
                    .Where(variable => variable.ReportSourceId == source.ReportSourceId)
                    .OrderBy(variable => variable.VariableKey, StringComparer.Ordinal)
                    .Select(variable => new ReportSourceVariable
                    {
                        VariableKey = variable.VariableKey,
                        MeasureKey = variable.MeasureKey,
                        Filter = ReportFilters.FromEngine(variable.SourceFilterJson),
                        MissingBehavior = variable.MissingBehavior
                    })
            ],
            report.SourceItems
                .Where(item => item.ReportSourceId == source.ReportSourceId)
                .GroupBy(item => item.RowKey, StringComparer.Ordinal)
                .OrderBy(row => row.Key, StringComparer.Ordinal)
                .ToDictionary(
                    row => row.Key,
                    row => (IReadOnlyList<ReportSourceRule>)[.. row.OrderBy(item => item.ItemKey, StringComparer.Ordinal).Select(item => new ReportSourceRule { ItemKey = item.ItemKey, Selection = item.Selection, Factor = item.Factor })],
                    StringComparer.Ordinal),
            [.. report.LeftOut.Where(item => item.ReportSourceId == source.ReportSourceId).Select(item => item.MemberKey).Order(StringComparer.Ordinal)]);
    }

    /// <summary>A profile with its picks by source key.</summary>
    private static ReportProfile Profile(ReportGraph report, SourceProfileRecord profile)
    {
        var sourceKeys = report.Sources.ToDictionary(source => source.ReportSourceId, source => source.SourceKey);
        return new ReportProfile
        {
            Key = profile.ProfileKey,
            DisplayName = profile.DisplayName,
            IsEnabled = profile.IsEnabled,
            Picks = report.Picks
                .Where(pick => pick.CalculationSourceProfileId == profile.CalculationSourceProfileId)
                .OrderBy(pick => pick.VariableKey, StringComparer.Ordinal)
                .ToDictionary(pick => pick.VariableKey, pick => sourceKeys[pick.ReportSourceId], StringComparer.Ordinal)
        };
    }

    /// <summary>A calculation dimension.</summary>
    private static ReportCalculationDimension CalculationDimension(CalculationDimensionRecord dimension)
    {
        return new ReportCalculationDimension
        {
            Key = dimension.DimensionKey,
            DisplayName = dimension.DisplayName,
            RoleId = dimension.DimensionRoleId,
            Grouping = GroupingOf(dimension),
            IsGroupable = dimension.IsGroupable,
            IsFilterable = dimension.IsFilterable,
            IsEnabled = dimension.IsEnabled
        };
    }

    /// <summary>A variable scope.</summary>
    private static ReportVariableScope Scope(ReportVariableScopeRecord scope)
    {
        return new ReportVariableScope { VariableKey = scope.VariableKey, ScopeMemberKey = scope.ScopeMemberKey, SelectionScope = scope.SelectionScope };
    }

    /// <summary>A column.</summary>
    private static ReportColumn Column(ReportColumnRecord column)
    {
        return new ReportColumn
        {
            Key = column.ColumnKey,
            DisplayName = column.DisplayName,
            ValueKind = column.ValueKind,
            MetricKey = column.MetricKey,
            DisplayFormat = column.DisplayFormat,
            IsVisible = column.IsVisible
        };
    }

    /// <summary>A style.</summary>
    private static ReportStyle Style(ReportStyleRecord style)
    {
        return new ReportStyle
        {
            Key = style.StyleKey,
            DisplayName = style.DisplayName,
            DefaultRole = style.DefaultRole,
            FontFamily = style.FontFamily,
            FontSize = style.FontSize,
            FontWeight = style.FontWeight,
            IsItalic = style.IsItalic,
            TextColor = style.TextColor,
            BackgroundColor = style.BackgroundColor,
            BorderTop = style.BorderTop,
            BorderBottom = style.BorderBottom,
            Indent = style.Indent
        };
    }
}
