namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A new report, as <c>POST reports</c> takes it: its fixed key plus its identity attributes.</summary>
public sealed record NewReportInput : ReportInput
{
    /// <summary>Stable key, unique across reports and fixed once created.</summary>
    public required string ReportKey { get; init; }
}
