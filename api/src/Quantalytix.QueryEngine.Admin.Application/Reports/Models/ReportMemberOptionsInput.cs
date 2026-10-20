namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The complete member options of an editable report version, as <c>PUT report-versions/{versionId}/member-options</c> takes them;
/// a member without an option shows its own label and values.
/// </summary>
public sealed record ReportMemberOptionsInput
{
    /// <summary>One option per member that shows differently.</summary>
    public required IReadOnlyList<ReportMemberOptionInput> Options { get; init; }
}
