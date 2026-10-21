namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Whether the engine offers a report, as <c>PUT reports/{reportId}/status</c> takes it.</summary>
public sealed record ReportStatusInput
{
    /// <summary>True enables the report, which the report rules must pass; false disables it.</summary>
    public required bool IsEnabled { get; init; }
}
