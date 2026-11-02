using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// A dataset version rule over a snapshot (blueprint 11, Rule codes). Every rule is a commit rule: the live version is what the engine
/// reads, so a finding shows in the preview and refuses the commit. The snapshot carries what the rules read outside the version, so
/// they read nothing.
/// </summary>
public abstract class DatasetRule : ISnapshotRule<DatasetSnapshot>
{
    /// <summary>Commit: findings refuse the save.</summary>
    public RuleStage Stage => RuleStage.Commit;

    /// <summary>The rule code its violations carry.</summary>
    protected abstract string Code { get; }

    /// <summary>Checks the snapshot.</summary>
    /// <param name="snapshot">The dataset version, stored or pending.</param>
    /// <param name="cancellationToken">Unused; the rule reads nothing.</param>
    /// <returns>The violations.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DatasetSnapshot snapshot, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<RuleViolation>>([.. Evaluate(snapshot)]);
    }

    /// <summary>The violations of a snapshot.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>The violations.</returns>
    protected abstract IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot);

    /// <summary>A violation of the rule.</summary>
    /// <param name="area">The <see cref="DatasetTabs"/> value to fix it on.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="itemId">The offending item; null for the settings or the version as a whole.</param>
    /// <param name="itemKey">The offending item's key.</param>
    /// <returns>The violation.</returns>
    protected RuleViolation Violation(string area, string message, long? itemId = null, string? itemKey = null)
    {
        return new RuleViolation { Code = Code, Message = message, Target = new ViolationTarget { Area = area, ItemId = itemId is { } id ? TemporaryIds.PresentStored(id) : null, ItemKey = itemKey } };
    }
}
