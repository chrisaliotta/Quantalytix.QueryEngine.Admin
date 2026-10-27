namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_source_left_out</c> row: a source member a report source deliberately leaves unmapped.</summary>
public sealed record ReportSourceLeftOutRecord
{
    /// <summary>The report source.</summary>
    public required long ReportSourceId { get; init; }

    /// <summary>The source dimension member key left out.</summary>
    public required string MemberKey { get; init; }
}
