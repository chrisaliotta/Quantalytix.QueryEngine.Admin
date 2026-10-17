namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// A new draft version of a report, as <c>POST reports/{reportId}/versions</c> takes it: the report dimension and model version
/// it reads, fixed once created, its editable attributes, and optionally a version whose lines and columns it copies.
/// </summary>
public sealed record NewReportVersionInput : ReportVersionInput
{
    /// <summary>The canonical dimension whose members the member lines name.</summary>
    public required long ReportDimensionId { get; init; }

    /// <summary>The calculation model version the report reads.</summary>
    public required long ModelVersionId { get; init; }

    /// <summary>
    /// A version of the same report whose lines and columns the draft copies, each metric followed by key into the draft's
    /// model version; null for an empty draft.
    /// </summary>
    public long? CloneFromVersionId { get; init; }
}
