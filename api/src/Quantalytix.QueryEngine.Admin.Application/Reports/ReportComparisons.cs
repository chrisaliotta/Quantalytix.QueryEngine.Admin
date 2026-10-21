using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads the default comparisons a report's settings store under <c>comparisons</c>: what a report contract 2.1 request compares the
/// run with when it names no comparisons, each in the request's comparison shape. Checks that shape as the contract defines it, each
/// problem at the JSON path of the value it concerns, and lists what each comparison names for the report rules to resolve. The
/// engine trusts stored defaults, so a shape that passes here is one it parses.
/// </summary>
public static class ReportComparisons
{
    /// <summary>The most comparisons a run makes; the engine refuses more.</summary>
    public const int MaxComparisons = 6;

    /// <summary>The key no comparison may take, because the engine names a breakout's totals query by it.</summary>
    private const string TotalsKey = "totals";

    /// <summary>The members each period type reads besides its <c>type</c>; any other member is refused, as the engine refuses it.</summary>
    private static readonly FrozenDictionary<string, string[]> PeriodMembers = new Dictionary<string, string[]>
    {
        [ComparisonPeriodTypes.Shift] = ["length", "unit"],
        [ComparisonPeriodTypes.Period] = ["periodicity", "offset"],
        [ComparisonPeriodTypes.PeriodEnd] = ["periodicity", "offset"],
        [ComparisonPeriodTypes.Range] = ["start", "endExclusive"]
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Checks the shape of a settings object's <c>comparisons</c>.</summary>
    /// <param name="comparisons">The value of the settings' <c>comparisons</c> member.</param>
    /// <returns>One problem per misshapen value, at its path inside the settings; none when the comparisons are well formed.</returns>
    public static IReadOnlyList<SettingsProblem> Problems(JsonElement comparisons)
    {
        var problems = new List<SettingsProblem>();
        if (comparisons.ValueKind != JsonValueKind.Array)
        {
            problems.Add(Problem("comparisons", "The comparisons must be an array of comparison objects."));
            return problems;
        }

        var items = comparisons.EnumerateArray().ToList();
        if (items.Count > MaxComparisons) problems.Add(Problem("comparisons", $"There are {items.Count} comparisons; a run makes at most {MaxComparisons}."));
        var keys = items.Select(item => item.ValueKind == JsonValueKind.Object ? Text(item, "key") : null).ToList();
        for (var index = 0; index < items.Count; index++) Comparison(items[index], index, keys, problems);
        return problems;
    }

    /// <summary>The default comparisons of a report's settings, which <see cref="Problems"/> found well formed.</summary>
    /// <param name="settingsJson">The report's default settings, or null.</param>
    /// <returns>What each comparison names, in order; none without comparisons.</returns>
    public static IReadOnlyList<DefaultComparison> Of(string? settingsJson)
    {
        if (settingsJson is null || !JsonSerializer.Deserialize<JsonElement>(settingsJson).TryGetProperty("comparisons", out var comparisons)) return [];
        return
        [
            .. comparisons.EnumerateArray().Select(comparison => new DefaultComparison
            {
                Key = Text(comparison, "key")!,
                ProfileKey = Text(comparison, "profileKey"),
                Scenario = Member(comparison, "scenario", out var scenario) ? ScenarioOf(scenario) : null,
                AssumptionSet = Text(comparison, "assumptionSet")
            })
        ];
    }

    /// <summary>Checks one comparison object, the <paramref name="index"/>th of the settings.</summary>
    private static void Comparison(JsonElement comparison, int index, IReadOnlyList<string?> keys, List<SettingsProblem> problems)
    {
        var path = $"comparisons[{index}]";
        var subject = $"Comparison {index + 1}";
        if (comparison.ValueKind != JsonValueKind.Object)
        {
            problems.Add(Problem(path, $"{subject} must be a comparison object."));
            return;
        }

        var key = Text(comparison, "key");
        if (key is null || !Regex.IsMatch(key, KeyRuleExtensions.KeyPattern) || key == TotalsKey)
            problems.Add(Problem($"{path}.key", $"{subject}'s key must start with a letter, hold only letters, digits and '_', and not be '{TotalsKey}'."));
        else if (keys.Count(other => other == key) > 1)
            problems.Add(Problem($"{path}.key", $"{subject}'s key '{key}' is the key of another comparison too."));
        foreach (var (name, noun) in new[] { ("profileKey", "profile"), ("assumptionSet", "assumption set") })
        {
            if (Member(comparison, name, out var value) && (value.ValueKind != JsonValueKind.String || value.GetString()!.Length == 0))
                problems.Add(Problem($"{path}.{name}", $"{subject}'s {noun} must be a non-empty key."));
        }

        if (Member(comparison, "base", out var baseKey)
            && (baseKey.ValueKind != JsonValueKind.String || baseKey.GetString() == key || !keys.Contains(baseKey.GetString())))
            problems.Add(Problem($"{path}.base", $"{subject}'s base must be the key of another comparison."));
        if (Member(comparison, "scenario", out var scenario) && ScenarioOf(scenario) is null)
            problems.Add(Problem($"{path}.scenario", $"{subject}'s scenario must be a member key, or {{ \"latest\": \"<folder key>\" }} for the latest version under a folder."));
        if (Member(comparison, "period", out var period)) Period(period, $"{path}.period", subject, problems);
        Transforms(comparison, $"{path}.transforms", subject, problems);
    }

    /// <summary>Checks a comparison's period rule: a known type with the members it reads, and no others.</summary>
    private static void Period(JsonElement period, string path, string subject, List<SettingsProblem> problems)
    {
        var type = period.ValueKind == JsonValueKind.Object ? Text(period, "type") : null;
        if (type is null || !PeriodMembers.TryGetValue(type, out var reads))
        {
            var types = string.Join(", ", ComparisonPeriodTypes.Definition.Values.Select(value => value.Value));
            problems.Add(Problem($"{path}.type", $"{subject}'s period must be a period object whose type is one of {types}."));
            return;
        }

        foreach (var member in period.EnumerateObject().Select(member => member.Name).Where(name => name != "type" && !reads.Contains(name)))
            problems.Add(Problem($"{path}.{member}", $"{subject}'s '{type}' period does not read '{member}'."));
        switch (type)
        {
            case ComparisonPeriodTypes.Shift:
                if (Integer(period, "length") is not >= 1) problems.Add(Problem($"{path}.length", $"{subject}'s shift length must be a whole number of 1 or more."));
                Periodicity(period, "unit", path, subject, problems);
                break;
            case ComparisonPeriodTypes.Range:
                var start = Date(period, "start");
                var endExclusive = Date(period, "endExclusive");
                if (start is null) problems.Add(Problem($"{path}.start", $"{subject}'s range start must be a date as yyyy-MM-dd."));
                if (endExclusive is null) problems.Add(Problem($"{path}.endExclusive", $"{subject}'s range end must be a date as yyyy-MM-dd."));
                if (endExclusive <= start) problems.Add(Problem($"{path}.endExclusive", $"{subject}'s range must end after it starts."));
                break;
            default:
                Periodicity(period, "periodicity", path, subject, problems);
                if (Member(period, "offset", out _) && Integer(period, "offset") is not <= 0)
                    problems.Add(Problem($"{path}.offset", $"{subject}'s offset must be a whole number of 0 or less, counted back from the period that holds the run period."));
                break;
        }
    }

    /// <summary>Checks a comparison's transforms: one or more distinct values of the <c>report_transform</c> vocabulary.</summary>
    private static void Transforms(JsonElement comparison, string path, string subject, List<SettingsProblem> problems)
    {
        string[] allowed = [.. ReportTransforms.Definition.Values.Select(value => value.Value)];
        if (!Member(comparison, "transforms", out var transforms) || transforms.ValueKind != JsonValueKind.Array || transforms.GetArrayLength() == 0
            || transforms.EnumerateArray().Any(transform => transform.ValueKind != JsonValueKind.String || !allowed.Contains(transform.GetString())))
            problems.Add(Problem(path, $"{subject} must list one or more transforms of {string.Join(", ", allowed)}."));
        else if (transforms.EnumerateArray().Select(transform => transform.GetString()).Distinct(StringComparer.Ordinal).Count() < transforms.GetArrayLength())
            problems.Add(Problem(path, $"{subject} must name each transform once."));
    }

    /// <summary>Checks that a period member is one of the <c>periodicity</c> vocabulary's values.</summary>
    private static void Periodicity(JsonElement period, string name, string path, string subject, List<SettingsProblem> problems)
    {
        string[] allowed = [.. Periodicities.Definition.Values.Select(value => value.Value)];
        if (!allowed.Contains(Text(period, name)))
            problems.Add(Problem($"{path}.{name}", $"{subject}'s {name} must be one of {string.Join(", ", allowed)}."));
    }

    /// <summary>A problem at a path inside the settings.</summary>
    private static SettingsProblem Problem(string path, string message)
    {
        return new SettingsProblem { Path = path, Message = message };
    }

    /// <summary>A member key, or <c>{ "latest": "&lt;folder key&gt;" }</c>; null for any other value.</summary>
    private static ScenarioSelection? ScenarioOf(JsonElement scenario)
    {
        if (scenario.ValueKind == JsonValueKind.String && scenario.GetString()!.Length > 0)
            return new ScenarioSelection { Key = scenario.GetString()!, Latest = false };
        if (scenario.ValueKind == JsonValueKind.Object && scenario.EnumerateObject().ToList() is [{ Name: "latest", Value.ValueKind: JsonValueKind.String } latest]
            && latest.Value.GetString()!.Length > 0)
            return new ScenarioSelection { Key = latest.Value.GetString()!, Latest = true };
        return null;
    }

    /// <summary>Whether an object has a member that is not JSON null, which the engine reads as absent.</summary>
    private static bool Member(JsonElement element, string name, out JsonElement value)
    {
        return element.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;
    }

    /// <summary>A string member's text; null when the member is absent or not a string.</summary>
    private static string? Text(JsonElement element, string name)
    {
        return Member(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>A whole-number member; null when the member is absent or not a 32-bit integer.</summary>
    private static int? Integer(JsonElement element, string name)
    {
        return Member(element, name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
    }

    /// <summary>A date member written as yyyy-MM-dd; null when the member is absent or not such a date.</summary>
    private static DateOnly? Date(JsonElement element, string name)
    {
        return DateOnly.TryParseExact(Text(element, name), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }
}
