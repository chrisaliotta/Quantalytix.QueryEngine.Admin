using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// A check every enabled report passes, because the engine trusts what it reads: it runs when a report is enabled, on every write
/// to an enabled report before the write, and, through <see cref="ReportConsumerRule"/>, when a release that enabled reports read is
/// validated; the rules that read a model's metrics also run, through <see cref="CalculationModels.ModelChanges"/>, on every write
/// to the model. A rule reads only the report and its scope, so it checks a report as a write would leave it.
/// </summary>
public interface IReportRule
{
    /// <summary>Checks one report.</summary>
    /// <param name="report">The report, as it stands or as a write would leave it.</param>
    /// <param name="scope">The release and dimension versions the report is checked against.</param>
    /// <returns>The violations found; none when the report passes.</returns>
    IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope);
}
