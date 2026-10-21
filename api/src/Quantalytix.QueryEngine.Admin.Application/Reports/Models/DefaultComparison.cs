namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>One of a report's default comparisons, reduced to what it names in the report and the tenant, which the report rules resolve.</summary>
public sealed record DefaultComparison
{
    /// <summary>The comparison's key, unique among the report's default comparisons.</summary>
    public required string Key { get; init; }

    /// <summary>The profile the comparison runs under; null for the run's.</summary>
    public string? ProfileKey { get; init; }

    /// <summary>The scenario the comparison reads; null for the run's.</summary>
    public ScenarioSelection? Scenario { get; init; }

    /// <summary>The captured assumption set the comparison reads the report as reported under; null for the active set.</summary>
    public string? AssumptionSet { get; init; }
}
