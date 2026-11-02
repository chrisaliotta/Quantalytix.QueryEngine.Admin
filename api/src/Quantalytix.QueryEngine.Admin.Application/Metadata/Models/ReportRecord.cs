namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_report</c> row: a report's identity and its own dimension. Its settings and content are versioned per snapshot in
/// <c>qe_report_version</c>.
/// </summary>
public sealed record ReportRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportId { get; init; }

    /// <summary>Stable logical report identity.</summary>
    public required string ReportKey { get; init; }

    /// <summary>The report's own dimension, whose members are its rows and into which every source classifies; fixed at creation.</summary>
    public required long ReportDimensionId { get; init; }
}
