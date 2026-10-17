namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_release_report</c> row: the report version a release pins for one report.</summary>
public sealed record ReleaseReportRecord
{
    /// <summary>The release; first part of the primary key.</summary>
    public required long ReleaseId { get; init; }

    /// <summary>The report; second part of the primary key.</summary>
    public required long ReportId { get; init; }

    /// <summary>The pinned version of the report.</summary>
    public required long ReportVersionId { get; init; }
}
