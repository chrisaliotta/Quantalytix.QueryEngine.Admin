using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The report change set that copies another report's content into a new report whose dimension copies the other's (OI-47): its
/// styles, columns, rows, sources with their variables, rules and left-out items, profiles, calculation dimensions, scenarios, scopes
/// and settings. Over another model, what does not resolve there is dropped: columns, formula rows, variables, picks, scopes and row
/// parameter values that name metrics or parameters the model lacks, and formula rows whose column was dropped.
/// </summary>
public static class ReportCopy
{
    /// <summary>The operations that give a new report the copied content.</summary>
    /// <param name="copied">The report copied.</param>
    /// <param name="model">The new report's model.</param>
    /// <returns>The operations, in an order whose every step names only what an earlier one created.</returns>
    public static IReadOnlyList<ReportOperation> Operations(ReportDocument copied, ModelGraph model)
    {
        var variables = model.VariableKeys;
        var measures = model.CalculatedMeasures.Select(measure => measure.MeasureKey).ToHashSet(StringComparer.Ordinal);
        var parameters = model.Parameters.Select(parameter => parameter.ParameterKey).ToHashSet(StringComparer.Ordinal);
        ReportColumn[] columns = [.. copied.Columns.Where(column => (column.ValueKind == ReportValueKinds.Variable ? variables : measures).Contains(column.MetricKey))];
        var columnKeys = columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
        ReportRow[] rows =
        [
            .. copied.Rows
                .Where(row => row.Kind != ReportRowKinds.Formula || (measures.Contains(row.CalculatedMeasureKey!) && columnKeys.Contains(row.ValueColumnKey!)))
                .Select(row => row with { Parameters = row.Parameters.Where(value => parameters.Contains(value.Key)).ToDictionary(StringComparer.Ordinal) })
        ];

        var settings = copied.Settings;
        return
        [
            .. copied.Styles.Select(style => new ReportSaveStyleOperation { Style = style }),
            new ReportSetColumnsOperation { Columns = columns },
            .. rows.Select(row => new ReportSaveRowOperation { Row = row, Index = rows.TakeWhile(earlier => earlier != row).Count(earlier => earlier.ParentKey == row.ParentKey) }),
            .. copied.Sources.SelectMany(source => SourceOperations(source, variables)),
            .. copied.Profiles.Select(profile => new ReportSaveProfileOperation
            {
                Profile = profile with { Picks = profile.Picks.Where(pick => variables.Contains(pick.Key)).ToDictionary(StringComparer.Ordinal) }
            }),
            .. copied.CalculationDimensions.Select(dimension => new ReportSaveCalculationDimensionOperation { Input = dimension }),
            new ReportSetScenariosOperation { RoleIds = [.. copied.Scenarios.Select(scenario => scenario.RoleId)] },
            new ReportSetVariableScopesOperation { Scopes = [.. copied.VariableScopes.Where(scope => variables.Contains(scope.VariableKey))] },
            new ReportUpdateSettingsOperation
            {
                Patch = new ReportSettingsPatch
                {
                    SubtotalPosition = settings.SubtotalPosition,
                    DefaultProfileKey = settings.DefaultProfileKey,
                    Periodicity = settings.Periodicity,
                    Layout = settings.Layout,
                    DefaultBreakout = settings.DefaultBreakout,
                    DefaultFilter = settings.DefaultFilter
                }
            }
        ];
    }

    /// <summary>A source, its variables the model has, and its rules and left-out items.</summary>
    private static IEnumerable<ReportOperation> SourceOperations(ReportSource source, IReadOnlySet<string> variables)
    {
        yield return new ReportSaveSourceOperation { Input = new ReportSourceInput
        {
            Key = source.Key,
            DisplayName = source.DisplayName,
            DatasetId = source.DatasetId,
            TimeFieldKey = source.TimeFieldKey,
            Method = source.Method,
            RoleId = source.RoleId,
            FieldKey = source.FieldKey
        } };
        yield return new ReportSetSourceVariablesOperation { SourceKey = source.Key, Variables = [.. source.Variables.Where(variable => variables.Contains(variable.VariableKey))] };
        foreach (var (rowKey, rules) in source.Rules) yield return new ReportSetRowRulesOperation { SourceKey = source.Key, RowKey = rowKey, Rules = rules };
        if (source.LeftOut.Count > 0) yield return new ReportSetLeftOutOperation { SourceKey = source.Key, ItemKeys = source.LeftOut };
    }
}
