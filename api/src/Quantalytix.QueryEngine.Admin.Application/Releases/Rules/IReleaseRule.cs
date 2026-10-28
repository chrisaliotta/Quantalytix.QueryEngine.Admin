using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// A release rule over a manifest held in memory, stored or pending. Unlike <see cref="ISnapshotRule{TSnapshot}"/> it answers
/// <see cref="ReleaseViolation"/>s, whose manifest row the release editor shows issue icons on; <see cref="ReleaseGateRule"/> and
/// <see cref="ReleaseVersionRules"/> hand the same findings to the change-set pipeline and the version workflow.
/// </summary>
public interface IReleaseRule
{
    /// <summary>When the rule's violations block: gates refuse a manifest edit, publication findings block only validate and publish.</summary>
    RuleStage Stage { get; }

    /// <summary>Checks a manifest.</summary>
    /// <param name="snapshot">The release, stored or pending.</param>
    /// <param name="cancellationToken">Cancels any reads of rows outside the snapshot.</param>
    /// <returns>The violations; empty when the manifest passes.</returns>
    Task<IReadOnlyList<ReleaseViolation>> EvaluateAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken);
}
