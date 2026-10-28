using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// Runs the release rules in the contract's order (<c>ReleaseRuleCode</c>): the manifest rules in registration order, then the report
/// rules of <see cref="ReleaseReportChecks"/>, over a stored release or a change set's pending manifest.
/// </summary>
/// <param name="rules">The manifest rules, in registration order.</param>
/// <param name="reports">The report rules over a release.</param>
/// <param name="time">Stamps the reports.</param>
public sealed class ReleaseRules(IEnumerable<IReleaseRule> rules, ReleaseReportChecks reports, TimeProvider time)
{
    /// <summary>The findings of the manifest rules of one stage.</summary>
    /// <param name="snapshot">The release, stored or pending.</param>
    /// <param name="stage">The stage.</param>
    /// <param name="cancellationToken">Cancels the rules' reads.</param>
    /// <returns>The findings, in rule order.</returns>
    public Task<IReadOnlyList<ReleaseViolation>> EvaluateAsync(ReleaseSnapshot snapshot, RuleStage stage, CancellationToken cancellationToken)
    {
        return RunAsync(rules.Where(rule => rule.Stage == stage), snapshot, cancellationToken);
    }

    /// <summary>The findings of every manifest rule.</summary>
    /// <param name="snapshot">The release, stored or pending.</param>
    /// <param name="cancellationToken">Cancels the rules' reads.</param>
    /// <returns>The findings, in rule order.</returns>
    public Task<IReadOnlyList<ReleaseViolation>> EvaluateAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        return RunAsync(rules, snapshot, cancellationToken);
    }

    /// <summary>The report of every release rule over a stored release.</summary>
    /// <param name="snapshot">The stored release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report; <c>unsaved</c> is false.</returns>
    public async Task<ReleaseCheckReport> CheckAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReleaseViolation> violations =
        [
            .. await EvaluateAsync(snapshot, cancellationToken),
            .. await reports.ConsumersAsync(snapshot.Release.SemanticReleaseId, cancellationToken)
        ];
        return Report(snapshot, false, violations);
    }

    /// <summary>The report of every release rule over a change set's pending manifest (OI-37).</summary>
    /// <param name="snapshot">The pending release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report; <c>unsaved</c> is true.</returns>
    public async Task<ReleaseCheckReport> CheckPendingAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReleaseViolation> violations =
        [
            .. await EvaluateAsync(snapshot, cancellationToken),
            .. await reports.PendingConsumersAsync(snapshot.Manifest.ToDictionary(row => row.DatasetId, row => row.DatasetVersionId), cancellationToken)
        ];
        return Report(snapshot, true, violations);
    }

    /// <summary>Runs some rules in order.</summary>
    private static async Task<IReadOnlyList<ReleaseViolation>> RunAsync(IEnumerable<IReleaseRule> applied, ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        var violations = new List<ReleaseViolation>();
        foreach (var rule in applied) violations.AddRange(await rule.EvaluateAsync(snapshot, cancellationToken));
        return violations;
    }

    /// <summary>A report of a snapshot's findings, stamped now.</summary>
    private ReleaseCheckReport Report(ReleaseSnapshot snapshot, bool unsaved, IReadOnlyList<ReleaseViolation> violations)
    {
        return new ReleaseCheckReport
        {
            ReleaseId = snapshot.Release.SemanticReleaseId,
            Unsaved = unsaved,
            CheckedAt = time.GetUtcNow().UtcDateTime,
            Violations = violations
        };
    }
}
