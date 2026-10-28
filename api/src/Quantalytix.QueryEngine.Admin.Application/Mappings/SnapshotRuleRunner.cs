using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Runs every snapshot rule of a mapping set or bridge, as <c>check</c>, freeze and the content read models do.</summary>
/// <typeparam name="TSnapshot">The aggregate's in-memory content.</typeparam>
/// <param name="rules">The snapshot rules, in registration order.</param>
public sealed class SnapshotRuleRunner<TSnapshot>(IEnumerable<ISnapshotRule<TSnapshot>> rules)
{
    /// <summary>The violations of every rule.</summary>
    /// <param name="snapshot">The version, stored or pending.</param>
    /// <param name="cancellationToken">Cancels the rules.</param>
    /// <returns>The violations, in rule order.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(TSnapshot snapshot, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var rule in rules) violations.AddRange(await rule.EvaluateAsync(snapshot, cancellationToken));
        return violations;
    }
}
