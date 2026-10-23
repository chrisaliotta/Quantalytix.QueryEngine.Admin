namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The complete scenarios of a report, as <c>PUT reports/{reportId}/scenarios</c> takes them; a run or comparison names one member
/// of each scenario its sources read.
/// </summary>
public sealed record ReportScenariosInput
{
    /// <summary>One entry per scenario role.</summary>
    public required IReadOnlyList<ReportScenarioInput> Scenarios { get; init; }
}
