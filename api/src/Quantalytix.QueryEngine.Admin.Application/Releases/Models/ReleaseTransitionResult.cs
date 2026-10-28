namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ReleaseTransitionResult</c>: the answer of <c>validate</c> and <c>publish</c>.</summary>
public sealed record ReleaseTransitionResult
{
    /// <summary>The release after the transition.</summary>
    public required Release Release { get; init; }

    /// <summary>The rule run that allowed it, with no violations.</summary>
    public required ReleaseCheckReport Report { get; init; }
}
