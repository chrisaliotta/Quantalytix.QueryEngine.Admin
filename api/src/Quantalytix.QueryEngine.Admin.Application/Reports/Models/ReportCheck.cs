using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>What checking a report against the report rules found; the check changes nothing.</summary>
public sealed record ReportCheck
{
    /// <summary>The rule violations; empty when the report could be enabled.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}
