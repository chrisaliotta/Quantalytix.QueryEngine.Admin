namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The complete column list of a draft report version, as <c>PUT report-versions/{versionId}/columns</c> takes it, in order.</summary>
public sealed record ReportColumnsInput
{
    /// <summary>The columns in their presentation order.</summary>
    public required IReadOnlyList<ReportColumnInput> Columns { get; init; }
}
