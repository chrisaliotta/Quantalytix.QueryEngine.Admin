using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// A dimension rule over a snapshot. A gate refuses a change set whose result breaks it; a publication rule is a preview finding
/// that blocks freezing. Every rule runs for <c>check</c> and freeze (<see cref="DimensionRules"/>). The snapshot carries what the
/// rules read outside the version, so they read nothing.
/// </summary>
public abstract class DimensionRule : ISnapshotRule<DimensionSnapshot>
{
    /// <summary>When the rule's violations block.</summary>
    public abstract RuleStage Stage { get; }

    /// <summary>The rule code its violations carry.</summary>
    protected abstract string Code { get; }

    /// <summary>Checks the snapshot.</summary>
    /// <param name="snapshot">The dimension version, stored or pending.</param>
    /// <param name="cancellationToken">Unused; the rule reads nothing.</param>
    /// <returns>The violations.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DimensionSnapshot snapshot, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<RuleViolation>>([.. Evaluate(snapshot)]);
    }

    /// <summary>The violations of a snapshot.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>The violations.</returns>
    protected abstract IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot);

    /// <summary>A violation of the rule.</summary>
    /// <param name="area">The <see cref="DimensionTabs"/> area to fix it in.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="itemId">The offending member, level or role, or a band ordinal; null for none.</param>
    /// <returns>The violation.</returns>
    protected RuleViolation Violation(string area, string message, long? itemId = null)
    {
        return new RuleViolation { Code = Code, Message = message, Target = new ViolationTarget { Area = area, ItemId = itemId } };
    }
}
