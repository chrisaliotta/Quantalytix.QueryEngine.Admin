using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Runs every dataset rule, as <c>check</c>, validate and publish do.</summary>
/// <param name="rules">The dataset snapshot rules, in registration order.</param>
public sealed class DatasetRules(IEnumerable<ISnapshotRule<DatasetSnapshot>> rules)
{
    /// <summary>The violations of every rule.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the rules.</param>
    /// <returns>The violations, in rule order.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DatasetSnapshot snapshot, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var rule in rules) violations.AddRange(await rule.EvaluateAsync(snapshot, cancellationToken));
        return violations;
    }
}
