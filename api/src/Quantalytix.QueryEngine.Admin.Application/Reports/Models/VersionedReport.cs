using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A report's identity with one snapshot's version of it, which holds its settings.</summary>
/// <param name="Report">The report identity.</param>
/// <param name="Version">The version.</param>
public sealed record VersionedReport(ReportRecord Report, ReportVersionRecord Version)
{
    /// <summary>The report's id.</summary>
    public long ReportId => Report.ReportId;

    /// <summary>The <c>report_key</c> of the report.</summary>
    public string ReportKey => Report.ReportKey;
}
