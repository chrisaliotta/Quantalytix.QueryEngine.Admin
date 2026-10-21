namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>A report source route: the report and one of its sources.</summary>
public sealed record ReportSourceReference
{
    /// <summary>The report that owns the source.</summary>
    public required long ReportId { get; init; }

    /// <summary>The source.</summary>
    public required long SourceId { get; init; }
}
