namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report</c> row: the stable identity of a report.</summary>
public sealed record ReportRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportId { get; init; }

    /// <summary>Stable logical report identity.</summary>
    public required string ReportKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification such as <c>margin_analysis</c>.</summary>
    public required string ReportType { get; init; }

    /// <summary>The canonical dimension whose members are the report's rows and into which every source classifies; fixed at creation.</summary>
    public required long ReportDimensionId { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Optional catalog payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }
}
