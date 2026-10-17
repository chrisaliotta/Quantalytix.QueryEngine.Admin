namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The editable identity attributes of a report, as <c>PUT reports/{reportId}</c> takes them.</summary>
public record ReportInput
{
    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification such as <c>margin_analysis</c>.</summary>
    public required string ReportType { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Optional catalog payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }
}
