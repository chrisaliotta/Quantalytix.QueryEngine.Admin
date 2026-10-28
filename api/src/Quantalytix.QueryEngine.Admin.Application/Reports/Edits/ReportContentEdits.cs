using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The operations on a report's profiles, calculation dimensions, scenarios, scopes, columns and styles. A save with
/// <c>originalKey</c> edits the stored item and may rename it, which renames what names it in the report's defaults; a save without
/// creates an item whose key is free. Deleting a calculation dimension removes it from the defaults (OI-56); deleting the default
/// profile or a style a row names leaves a reference the gate refuses.
/// </summary>
internal static class ReportContentEdits
{
    /// <summary>Creates or edits a profile with its complete picks.</summary>
    public static ReportGraph SaveProfile(ReportGraph report, ReportSaveProfileOperation save, ChangeContext context)
    {
        var profile = save.Profile;
        var stored = Saved(report.Profiles, item => item.ProfileKey, save.OriginalKey, profile.Key, "profile", "profile.key", context);
        var sourceIds = report.Sources.ToDictionary(source => source.SourceKey, source => source.ReportSourceId, StringComparer.Ordinal);
        foreach (var (variableKey, sourceKey) in profile.Picks.Where(pick => !sourceIds.ContainsKey(pick.Value)))
            context.Reject($"profile.picks.{variableKey}", $"Profile '{profile.Key}' picks '{sourceKey}' for '{variableKey}', which is no source of the report.");
        if (context.Refused) return report;

        var profileId = stored?.CalculationSourceProfileId ?? context.Ids.NewId();
        var record = new SourceProfileRecord
        {
            CalculationSourceProfileId = profileId, ReportId = report.Report.ReportId, ProfileKey = profile.Key, DisplayName = profile.DisplayName, IsEnabled = profile.IsEnabled
        };
        var renamesDefault = save.OriginalKey is not null && report.Report.DefaultProfileKey == save.OriginalKey;
        return report with
        {
            Report = renamesDefault ? report.Report with { DefaultProfileKey = profile.Key } : report.Report,
            Profiles = [.. report.Profiles.Where(other => other.CalculationSourceProfileId != profileId), record],
            Picks =
            [
                .. report.Picks.Where(pick => pick.CalculationSourceProfileId != profileId),
                .. profile.Picks.Select(pick => new SourceProfileVariableRecord { CalculationSourceProfileId = profileId, VariableKey = pick.Key, ReportSourceId = sourceIds[pick.Value] })
            ]
        };
    }

    /// <summary>Removes a profile with its picks.</summary>
    public static ReportGraph DeleteProfile(ReportGraph report, ReportDeleteProfileOperation delete, ChangeContext context)
    {
        if (ReportEdits.Named(report.Profiles, profile => profile.ProfileKey, delete.Key, "profile", context) is not { } profile) return report;
        return report with
        {
            Profiles = [.. report.Profiles.Where(other => other != profile)],
            Picks = [.. report.Picks.Where(pick => pick.CalculationSourceProfileId != profile.CalculationSourceProfileId)]
        };
    }

    /// <summary>Creates or edits a calculation dimension; a rename renames it in the default breakout and filter.</summary>
    public static ReportGraph SaveDimension(ReportSnapshot snapshot, ReportSaveCalculationDimensionOperation save, ChangeContext context)
    {
        var report = snapshot.Content;
        var input = save.Input;
        var stored = Saved(report.Dimensions, item => item.DimensionKey, save.OriginalKey, input.Key, "calculation dimension", "input.key", context);
        if (snapshot.Surroundings.Roles.All(role => role.DimensionRoleId != input.RoleId)) context.Reject("input.roleId", $"Role {input.RoleId} does not exist.");
        if (context.Refused) return report;

        var record = new CalculationDimensionRecord
        {
            CalculationDimensionId = stored?.CalculationDimensionId ?? context.Ids.NewId(),
            ReportId = report.Report.ReportId,
            DimensionKey = input.Key,
            DisplayName = input.DisplayName,
            DimensionRoleId = input.RoleId,
            GroupingMode = input.Grouping switch
            {
                GroupingLevel => GroupingModes.Level,
                GroupingAncestor => GroupingModes.Ancestor,
                GroupingDepth => GroupingModes.Depth,
                _ => GroupingModes.Member
            },
            GroupingLevelKey = (input.Grouping as GroupingLevel)?.LevelKey,
            GroupingAncestorMemberKey = (input.Grouping as GroupingAncestor)?.MemberKey,
            GroupingDepth = (input.Grouping as GroupingDepth)?.Depth,
            IsGroupable = input.IsGroupable,
            IsFilterable = input.IsFilterable,
            IsEnabled = input.IsEnabled
        };
        var renamed = stored is not null && stored.DimensionKey != input.Key;
        return report with
        {
            Report = renamed ? ReportSettingsColumns.WithDimension(report.Report, stored!.DimensionKey, input.Key) : report.Report,
            Dimensions = [.. report.Dimensions.Where(other => other.CalculationDimensionId != record.CalculationDimensionId), record]
        };
    }

    /// <summary>Removes a calculation dimension and its key from the default breakout and filter.</summary>
    public static ReportGraph DeleteDimension(ReportGraph report, ReportDeleteCalculationDimensionOperation delete, ChangeContext context)
    {
        if (ReportEdits.Named(report.Dimensions, dimension => dimension.DimensionKey, delete.Key, "calculation dimension", context) is not { } dimension) return report;
        return report with
        {
            Report = ReportSettingsColumns.WithDimension(report.Report, delete.Key, null),
            Dimensions = [.. report.Dimensions.Where(other => other != dimension)]
        };
    }

    /// <summary>
    /// Replaces the scenarios. Requests name a scenario by its role, and every dataset carries a scenario's members through that one
    /// role, so each role must be the only role of its dimension.
    /// </summary>
    public static ReportGraph SetScenarios(ReportSnapshot snapshot, ReportSetScenariosOperation set, ChangeContext context)
    {
        var roles = snapshot.Surroundings.Roles;
        for (var index = 0; index < set.RoleIds.Count; index++)
        {
            var roleId = set.RoleIds[index];
            if (roles.FirstOrDefault(role => role.DimensionRoleId == roleId) is not { } role)
            {
                context.Reject($"roleIds[{index}]", $"Role {roleId} does not exist.");
                continue;
            }

            string[] others = [.. roles.Where(other => other.DimensionId == role.DimensionId && other != role).Select(other => $"'{other.DimensionRoleKey}'").Order(StringComparer.Ordinal)];
            if (others.Length > 0)
                context.Reject($"roleIds[{index}]", $"Role '{role.DimensionRoleKey}' shares its dimension with {string.Join(", ", others)}, and a scenario's dimension has exactly one role.");
        }

        if (context.Refused) return snapshot.Content;
        var report = snapshot.Content;
        return report with { Scenarios = [.. set.RoleIds.Select(roleId => new ReportScenarioRecord { ReportId = report.Report.ReportId, DimensionRoleId = roleId })] };
    }

    /// <summary>Replaces the variable scopes, one per variable (OI-50).</summary>
    public static ReportGraph SetScopes(ReportGraph report, ReportSetVariableScopesOperation set)
    {
        return report with
        {
            Scopes =
            [
                .. set.Scopes.Select(scope => new ReportVariableScopeRecord
                {
                    ReportId = report.Report.ReportId, VariableKey = scope.VariableKey, ScopeMemberKey = scope.ScopeMemberKey, SelectionScope = scope.SelectionScope
                })
            ]
        };
    }

    /// <summary>
    /// Replaces the columns in order. A column keeps its id by key, so the formula rows that place values in it keep pointing at it;
    /// a column a formula row places its value in must stay.
    /// </summary>
    public static ReportGraph SetColumns(ReportGraph report, ReportSetColumnsOperation set, ChangeContext context)
    {
        var stored = report.Columns.ToDictionary(column => column.ColumnKey, StringComparer.Ordinal);
        var kept = set.Columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
        var columnKeys = report.Columns.ToDictionary(column => column.ReportColumnId, column => column.ColumnKey);
        foreach (var line in report.Lines.Where(line => line.ValueReportColumnId is { } id && !kept.Contains(columnKeys[id])))
            context.Reject("columns", $"Formula row '{line.LineKey}' places its value in column '{columnKeys[line.ValueReportColumnId!.Value]}', so the column must stay.");
        if (context.Refused) return report;

        return report with
        {
            Columns =
            [
                .. set.Columns.Select((column, index) => new ReportColumnRecord
                {
                    ReportColumnId = stored.GetValueOrDefault(column.Key)?.ReportColumnId ?? context.Ids.NewId(),
                    ReportId = report.Report.ReportId,
                    ColumnKey = column.Key,
                    DisplayName = column.DisplayName,
                    ValueKind = column.ValueKind,
                    MetricKey = column.MetricKey,
                    SortOrder = index + 1,
                    DisplayFormat = column.DisplayFormat,
                    IsVisible = column.IsVisible,
                    PayloadJson = stored.GetValueOrDefault(column.Key)?.PayloadJson
                })
            ]
        };
    }

    /// <summary>Creates or replaces a style by key.</summary>
    public static ReportGraph SaveStyle(ReportGraph report, ReportSaveStyleOperation save)
    {
        var style = save.Style;
        var record = new ReportStyleRecord
        {
            ReportId = report.Report.ReportId,
            StyleKey = style.Key,
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
        return report with { Styles = [.. report.Styles.Where(other => other.StyleKey != style.Key), record] };
    }

    /// <summary>Removes a style.</summary>
    public static ReportGraph DeleteStyle(ReportGraph report, ReportDeleteStyleOperation delete, ChangeContext context)
    {
        if (ReportEdits.Named(report.Styles, style => style.StyleKey, delete.Key, "style", context) is not { } style) return report;
        return report with { Styles = [.. report.Styles.Where(other => other != style)] };
    }

    /// <summary>
    /// The stored item a save edits: none for a new item, whose key must be free; the item <paramref name="originalKey"/> names
    /// otherwise, whose new key must be its own or free. Refuses the operation, or raises the conflict, when not.
    /// </summary>
    private static T? Saved<T>(IReadOnlyList<T> items, Func<T, string> keyOf, string? originalKey, string key, string noun, string path, ChangeContext context) where T : class
    {
        var stored = originalKey is null ? null : ReportEdits.Named(items, keyOf, originalKey, noun, context);
        if (key != originalKey && items.Any(item => keyOf(item) == key)) context.Reject(path, $"The report already has a {noun} with key '{key}'.");
        return stored;
    }
}
