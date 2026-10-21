namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// A new report, as <c>POST reports</c> takes it: its fixed key, its identity attributes, and optionally the dimension whose
/// published rows its own report dimension starts as a copy of.
/// </summary>
public sealed record NewReportInput : ReportInput
{
    /// <summary>Stable key, unique across reports and fixed once created; the report's own dimension takes it too.</summary>
    public required string ReportKey { get; init; }

    /// <summary>
    /// A categorical or hierarchical dimension with one root, whose published members and levels the report dimension copies;
    /// null for a report dimension with a single root row.
    /// </summary>
    public long? StartFromDimensionId { get; init; }
}
