using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>
/// Runs a kind's existing <see cref="IPublicationRule"/>s, unchanged, as one publication-stage snapshot rule, until the module
/// migrates them to <see cref="ISnapshotRule{TSnapshot}"/>. The existing rules read the stored version by id, so the adapter
/// checks the version the snapshot came from as stored, not a change set's pending edits: right for freeze, publish and a
/// read-only check of a stored snapshot; a preview's findings lag the pending edits until the rules move.
/// </summary>
/// <typeparam name="TSnapshot">A snapshot of one version.</typeparam>
/// <param name="workflow">Runs the kind's publication rules with the version counted as published, as a transition does.</param>
public sealed class VersionRuleAdapter<TSnapshot>(VersionWorkflow workflow) : ISnapshotRule<TSnapshot> where TSnapshot : IVersionSnapshot
{
    /// <summary>Publication: the adapted rules guard leaving draft.</summary>
    public RuleStage Stage => RuleStage.Publication;

    /// <summary>Runs every publication rule of the snapshot's kind on its stored version.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="cancellationToken">Cancels the rules' reads.</param>
    /// <returns>The violations, in rule registration order.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(TSnapshot snapshot, CancellationToken cancellationToken)
    {
        return workflow.CheckAsync(snapshot.Kind, snapshot.VersionId, cancellationToken);
    }
}
