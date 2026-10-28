using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// A check every enabled report passes, because the engine trusts what it reads: it runs when a report is enabled, on every change
/// set of a report (a gate while the report is enabled, findings otherwise), when a release that enabled reports read is checked,
/// and, through <see cref="ReportModelChecks"/>, on every change set of the model a report reads. A rule reads only the report and
/// its scope, so it checks a report as a change would leave it.
/// </summary>
public interface IReportRule
{
    /// <summary>Checks one report.</summary>
    /// <param name="report">The report, as it stands or as a change would leave it.</param>
    /// <param name="scope">The release and dimension versions the report is checked against.</param>
    /// <returns>The violations found; none when the report passes.</returns>
    IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope);
}
