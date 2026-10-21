namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The result of a requested status transition of one version, or of a set of versions that moves together.</summary>
public sealed record TransitionResult
{
    /// <summary>How the transition ended.</summary>
    public required TransitionOutcome Outcome { get; init; }

    /// <summary>
    /// Each version's state afterwards, in the order requested: the new states when applied, otherwise the unchanged current states.
    /// </summary>
    public required IReadOnlyList<VersionState> States { get; init; }

    /// <summary>The rule violations when rejected; empty otherwise.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}
