namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A problem with a value inside a report's default settings, at the JSON path of the value inside the settings.</summary>
public sealed record SettingsProblem
{
    /// <summary>The path inside the settings, such as <c>comparisons[1].scenario</c>.</summary>
    public required string Path { get; init; }

    /// <summary>What is wrong with the value.</summary>
    public required string Message { get; init; }
}
