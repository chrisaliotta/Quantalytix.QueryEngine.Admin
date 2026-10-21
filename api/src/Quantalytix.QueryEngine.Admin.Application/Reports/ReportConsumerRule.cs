using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks every enabled report against a report rule when a release is validated, because activating the release changes the
/// dataset versions those reports read: <c>report_sources_resolve</c> and <c>report_factors_apply</c>, against the release's own.
/// </summary>
/// <param name="rule">The report rule.</param>
/// <param name="reader">Reads the reports and their scope.</param>
public sealed class ReportConsumerRule(IReportRule rule, ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The kind the rule checks: semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Checks every enabled report against the release's dataset versions.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var reports = await reader.EnabledAsync(cancellationToken);
        var scope = await reader.ScopeAsync(reports, versionId, cancellationToken);
        return [.. reports.SelectMany(report => rule.Evaluate(report, scope))];
    }
}
