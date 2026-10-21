using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks the enabled reports a model version or a release being validated serves against a report rule, because publishing the
/// version or activating the release changes what those reports read: a model version against <c>report_metrics_resolve</c> and
/// <c>profiles_complete</c>, a release against <c>report_sources_resolve</c>.
/// </summary>
/// <param name="kind">The kind being validated: <c>calculation_model_version</c> or <c>semantic_release</c>.</param>
/// <param name="rule">The report rule.</param>
/// <param name="reader">Reads the reports and their scope.</param>
public sealed class ReportConsumerRule(string kind, IReportRule rule, ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The kind the rule checks.</summary>
    public string Kind { get; } = kind;

    /// <summary>Checks every enabled report the version serves.</summary>
    /// <param name="versionId">The version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var reports = await reader.ConsumersAsync(Kind, versionId, cancellationToken);
        var scope = await reader.ScopeAsync(reports, Kind == VersionKinds.SemanticRelease ? versionId : null, cancellationToken);
        return [.. reports.SelectMany(report => rule.Evaluate(report, scope))];
    }
}
