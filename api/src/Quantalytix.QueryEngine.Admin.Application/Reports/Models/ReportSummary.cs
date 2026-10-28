namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The contract's <c>ReportSummary</c>: one report of the library.</summary>
public sealed record ReportSummary
{
    /// <summary>The report.</summary>
    public required long Id { get; init; }

    /// <summary>The report's fixed key.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification such as <c>financial</c>.</summary>
    public required string ReportType { get; init; }

    /// <summary>The calculation model the report reads.</summary>
    public required long ModelId { get; init; }

    /// <summary>The model's key.</summary>
    public required string ModelKey { get; init; }

    /// <summary>Whether the engine offers the report.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The default periodicity; null when the report names none.</summary>
    public required string? Periodicity { get; init; }

    /// <summary>How many source profiles the report has.</summary>
    public required int ProfileCount { get; init; }

    /// <summary>How many sources the report has.</summary>
    public required int SourceCount { get; init; }
}
