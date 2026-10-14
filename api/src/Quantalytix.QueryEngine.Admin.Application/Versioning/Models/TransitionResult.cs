namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The result of a requested status transition.</summary>
public sealed record TransitionResult
{
    /// <summary>How the transition ended.</summary>
    public required TransitionOutcome Outcome { get; init; }

    /// <summary>The version's state afterwards: the new state when applied, otherwise the unchanged current state.</summary>
    public required VersionState State { get; init; }

    /// <summary>The rule violations when rejected; empty otherwise.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}
