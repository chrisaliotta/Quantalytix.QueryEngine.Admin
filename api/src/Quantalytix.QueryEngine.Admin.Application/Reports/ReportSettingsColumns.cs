using System.Text.Json.Nodes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Maps the typed <see cref="ReportSettings"/> onto the report row (OI-54): <c>periodicity</c> and <c>layout</c> in
/// <c>default_settings_json</c> beside the settings the editor does not show (<c>timeZone</c>), the breakout as
/// <c>default_pivot_json.rowDimensions</c> beside its <c>columnDimensions</c>, and the filter in the engine's shape in
/// <c>default_filter_json</c>.
/// </summary>
public static class ReportSettingsColumns
{
    /// <summary>The settings a new report starts with: monthly periods in UTC.</summary>
    public const string DefaultSettingsJson = """{"periodicity":"month","timeZone":"UTC"}""";

    /// <summary>The pivot property that holds the breakout.</summary>
    private const string RowDimensions = "rowDimensions";

    /// <summary>The settings of a report row.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The typed settings.</returns>
    public static ReportSettings Read(ReportRecord report)
    {
        var settings = Object(report.DefaultSettingsJson);
        return new ReportSettings
        {
            DisplayName = report.DisplayName,
            ReportType = report.ReportType,
            SubtotalPosition = report.SubtotalPosition,
            DefaultProfileKey = report.DefaultProfileKey,
            Periodicity = (string?)settings["periodicity"],
            Layout = (string?)settings["layout"] ?? ReportLayouts.Statement,
            DefaultBreakout = Breakout(report.DefaultPivotJson),
            DefaultFilter = ReportFilters.FromEngine(report.DefaultFilterJson)
        };
    }

    /// <summary>A report row with a patch's settings.</summary>
    /// <param name="report">The report.</param>
    /// <param name="patch">The settings to change.</param>
    /// <returns>The report as the patch leaves it.</returns>
    public static ReportRecord Apply(ReportRecord report, ReportSettingsPatch patch)
    {
        var settings = Object(report.DefaultSettingsJson);
        if (patch.SetsPeriodicity) settings["periodicity"] = patch.Periodicity;
        if (patch.Layout is not null) settings["layout"] = patch.Layout;
        return report with
        {
            DisplayName = patch.DisplayName ?? report.DisplayName,
            ReportType = patch.ReportType ?? report.ReportType,
            SubtotalPosition = patch.SubtotalPosition ?? report.SubtotalPosition,
            DefaultProfileKey = patch.SetsDefaultProfileKey ? patch.DefaultProfileKey : report.DefaultProfileKey,
            DefaultSettingsJson = settings.ToJsonString(),
            DefaultPivotJson = patch.DefaultBreakout is { } breakout ? WithBreakout(report.DefaultPivotJson, breakout) : report.DefaultPivotJson,
            DefaultFilterJson = patch.SetsDefaultFilter ? ReportFilters.ToEngine(patch.DefaultFilter) : report.DefaultFilterJson
        };
    }

    /// <summary>A report row with a calculation dimension renamed in its pivot and filter, or removed from them when the new key is null.</summary>
    /// <param name="report">The report.</param>
    /// <param name="key">The dimension's key.</param>
    /// <param name="renamedTo">Its new key; null when it is deleted.</param>
    /// <returns>The report with its defaults rewritten.</returns>
    public static ReportRecord WithDimension(ReportRecord report, string key, string? renamedTo)
    {
        return report with
        {
            DefaultPivotJson = Pivot(report.DefaultPivotJson, keys => [.. keys.Where(name => name != key || renamedTo is not null).Select(name => name == key ? renamedTo! : name)]),
            DefaultFilterJson = renamedTo is null
                ? ReportFilters.WithoutDimension(report.DefaultFilterJson, key)
                : ReportFilters.RenameDimension(report.DefaultFilterJson, key, renamedTo)
        };
    }

    /// <summary>The breakout of a stored pivot: its row dimensions.</summary>
    private static IReadOnlyList<string> Breakout(string? pivotJson)
    {
        return Object(pivotJson)[RowDimensions] is JsonArray keys ? [.. keys.Select(key => (string)key!)] : [];
    }

    /// <summary>A stored pivot with new row dimensions.</summary>
    private static string? WithBreakout(string? pivotJson, IReadOnlyList<string> breakout)
    {
        var pivot = Object(pivotJson);
        pivot[RowDimensions] = new JsonArray([.. breakout.Select(key => JsonValue.Create(key))]);
        return pivot.ToJsonString();
    }

    /// <summary>A stored pivot with each of its dimension lists rewritten; null stays null.</summary>
    private static string? Pivot(string? pivotJson, Func<IEnumerable<string>, string[]> rewrite)
    {
        if (pivotJson is null) return null;
        var pivot = Object(pivotJson);
        foreach (var name in pivot.Select(property => property.Key).ToList())
            pivot[name] = new JsonArray([.. rewrite(pivot[name]!.AsArray().Select(key => (string)key!)).Select(key => JsonValue.Create(key))]);
        return pivot.ToJsonString();
    }

    /// <summary>A stored JSON object, or an empty one for null.</summary>
    private static JsonObject Object(string? json)
    {
        return json is null ? [] : JsonNode.Parse(json)!.AsObject();
    }
}
