namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The complete overlay rows of an editable report version, as <c>PUT report-versions/{versionId}/lines</c> takes them: each overlay
/// parent before its children. The rows sit among the rows of the report dimension's members by parent key and sort order.
/// </summary>
public sealed record ReportLinesInput
{
    /// <summary>The overlay rows, each overlay parent before its children.</summary>
    public required IReadOnlyList<ReportLineInput> Lines { get; init; }
}
