namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A report style route: the report and the key of one of its styles, which a write creates when it is new.</summary>
public sealed record ReportStyleReference
{
    /// <summary>The report that owns the style.</summary>
    public required long ReportId { get; init; }

    /// <summary>The style's key.</summary>
    public required string StyleKey { get; init; }
}
