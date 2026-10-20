namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The complete variable targets of an editable report version, as <c>PUT report-versions/{versionId}/variable-targets</c> takes
/// them; a variable without a target reads every row its sources classify.
/// </summary>
public sealed record ReportVariableTargetsInput
{
    /// <summary>One target per targeted variable.</summary>
    public required IReadOnlyList<ReportVariableTargetInput> Targets { get; init; }
}
