using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The contract's <c>ReportChangeSetPreview</c>: the kernel preview with the pending report's diagnostics.</summary>
public sealed record ReportChangeSetPreview
{
    /// <summary>The report as the operations leave it.</summary>
    public required ReportDocument State { get; init; }

    /// <summary>The report rules' findings on the pending report; empty for an enabled report, which the gate keeps passing.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }

    /// <summary>The pending report's readiness, coverage and profile resolution.</summary>
    public required ReportDiagnostics Diagnostics { get; init; }
}
