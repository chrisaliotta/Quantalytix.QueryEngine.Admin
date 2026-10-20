namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The complete variable scopes of an editable report version, as <c>PUT report-versions/{versionId}/variable-scopes</c> takes
/// them; a variable without a scope reads every row its sources classify.
/// </summary>
public sealed record ReportVariableScopesInput
{
    /// <summary>One scope per scoped variable.</summary>
    public required IReadOnlyList<ReportVariableScopeInput> Scopes { get; init; }
}
