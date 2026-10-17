namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The complete line tree of a draft report version, as <c>PUT report-versions/{versionId}/lines</c> takes it: lines in
/// pre-order, each parent before its children, siblings in their presentation order.
/// </summary>
public sealed record ReportLinesInput
{
    /// <summary>The lines in pre-order.</summary>
    public required IReadOnlyList<ReportLineInput> Lines { get; init; }
}
