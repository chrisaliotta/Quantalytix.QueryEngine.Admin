namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Runs a kind's gate and commit rules on a stored version, resolving what it names in the version's own snapshot. Restore runs
/// them on every version of the snapshot it restores, which is what live would hold, and refuses when any finds a violation. Each
/// versioned module implements one.
/// </summary>
public interface IVersionRules
{
    /// <summary>The <c>SnapshotKind</c> the rules check.</summary>
    string Kind { get; }

    /// <summary>Checks one stored version.</summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the reads and the rules.</param>
    /// <returns>The violations; empty when the version passes.</returns>
    Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken);
}
