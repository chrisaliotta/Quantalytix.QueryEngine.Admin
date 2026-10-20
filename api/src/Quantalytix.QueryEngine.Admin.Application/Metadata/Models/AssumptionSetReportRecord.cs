namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_assumption_set_report</c> row: the version of one report an assumption set uses.</summary>
public sealed record AssumptionSetReportRecord
{
    /// <summary>The owning assumption set; first part of the primary key.</summary>
    public required long AssumptionSetId { get; init; }

    /// <summary>The report; second part of the primary key.</summary>
    public required long ReportId { get; init; }

    /// <summary>The pinned version: published in the active set, published or retired in a historical set.</summary>
    public required long ReportVersionId { get; init; }
}
