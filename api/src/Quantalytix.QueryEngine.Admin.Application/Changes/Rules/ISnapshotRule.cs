using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>
/// A semantic check of an aggregate held in memory, replacing <see cref="IVersionRule"/>'s check of a stored version by id: the
/// same rule then checks a change set's pending result, a stored version before freeze or publish, and a read-only check.
/// Register with <see cref="ChangesServiceCollectionExtensions.AddSnapshotRule{TSnapshot, TRule}"/>; rules run in registration order.
/// </summary>
/// <typeparam name="TSnapshot">The aggregate's in-memory content.</typeparam>
public interface ISnapshotRule<in TSnapshot>
{
    /// <summary>When the rule's violations block.</summary>
    RuleStage Stage { get; }

    /// <summary>Checks the aggregate.</summary>
    /// <param name="snapshot">The aggregate, stored or pending.</param>
    /// <param name="cancellationToken">Cancels any reads of rows outside the aggregate.</param>
    /// <returns>The violations; empty when the aggregate passes.</returns>
    Task<IReadOnlyList<RuleViolation>> EvaluateAsync(TSnapshot snapshot, CancellationToken cancellationToken);
}
