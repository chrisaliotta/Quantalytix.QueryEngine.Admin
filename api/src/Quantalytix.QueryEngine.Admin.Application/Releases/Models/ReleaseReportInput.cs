namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>One report pin: the report version a release pins for a report.</summary>
public sealed record ReleaseReportInput
{
    /// <summary>The report.</summary>
    public required long ReportId { get; init; }

    /// <summary>The version of the report that the release pins.</summary>
    public required long ReportVersionId { get; init; }
}
