namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A new report, as <c>POST reports</c> takes it: its fixed key and report dimension plus its identity attributes.</summary>
public sealed record NewReportInput : ReportInput
{
    /// <summary>Stable key, unique across reports and fixed once created.</summary>
    public required string ReportKey { get; init; }

    /// <summary>The canonical dimension whose members are the report's rows and into which every source classifies; fixed once created.</summary>
    public required long ReportDimensionId { get; init; }
}
