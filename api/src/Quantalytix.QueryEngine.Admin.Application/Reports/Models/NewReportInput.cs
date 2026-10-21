namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// A new report, as <c>POST reports</c> takes it: its fixed key and model, its identity attributes, and optionally the dimension
/// whose published rows its own report dimension starts as a copy of. A new report starts disabled.
/// </summary>
public sealed record NewReportInput : ReportInput
{
    /// <summary>Stable key, unique across reports and fixed once created; the report's own dimension takes it too.</summary>
    public required string ReportKey { get; init; }

    /// <summary>The calculation model whose published version the report reads; it must have one, and it never changes.</summary>
    public required long ModelId { get; init; }

    /// <summary>
    /// A categorical or hierarchical dimension with one root, whose published members and levels the report dimension copies;
    /// null for a report dimension with a single root row.
    /// </summary>
    public long? StartFromDimensionId { get; init; }
}
