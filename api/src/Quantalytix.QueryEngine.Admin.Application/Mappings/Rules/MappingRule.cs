using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// A mapping version rule over a snapshot. Every mapping rule is a publication finding: a preview reports it on the entry at fault so
/// the editor can flag it, and <see cref="LiveChanges"/> refuses the commit while one remains, because the version is live (B11).
/// Every rule runs for <c>check</c> and freeze (<see cref="SnapshotRuleRunner{TSnapshot}"/>). The snapshot carries what the rules read outside the
/// version, so they read nothing.
/// </summary>
public abstract class MappingRule : ISnapshotRule<MappingSetSnapshot>
{
    /// <summary>Publication: a finding the editor flags, which refuses the commit.</summary>
    public RuleStage Stage => RuleStage.Publication;

    /// <summary>Checks the snapshot.</summary>
    /// <param name="snapshot">The mapping version, stored or pending.</param>
    /// <param name="cancellationToken">Unused; the rule reads nothing.</param>
    /// <returns>The violations.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(MappingSetSnapshot snapshot, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<RuleViolation>>([.. Evaluate(snapshot)]);
    }

    /// <summary>The violations of a snapshot.</summary>
    /// <param name="snapshot">The mapping version.</param>
    /// <returns>The violations, in entry order.</returns>
    protected abstract IEnumerable<RuleViolation> Evaluate(MappingSetSnapshot snapshot);
}
