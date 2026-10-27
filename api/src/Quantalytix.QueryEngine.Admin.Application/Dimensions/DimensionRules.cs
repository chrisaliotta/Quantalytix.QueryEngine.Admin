using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Runs every dimension rule, gates and publication rules alike, as <c>check</c> and freeze do.</summary>
/// <param name="rules">The dimension snapshot rules, in registration order.</param>
public sealed class DimensionRules(IEnumerable<ISnapshotRule<DimensionSnapshot>> rules)
{
    /// <summary>The violations of every rule.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the rules.</param>
    /// <returns>The violations, in rule order.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DimensionSnapshot snapshot, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var rule in rules) violations.AddRange(await rule.EvaluateAsync(snapshot, cancellationToken));
        return violations;
    }
}
