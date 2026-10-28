namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// The contract's <c>ReleaseChangeSetPreview</c>: the kernel preview with the release rules' report over the pending manifest and its
/// difference from the active release (OI-37).
/// </summary>
public sealed record ReleaseChangeSetPreview
{
    /// <summary>The release with the pending manifest.</summary>
    public required ReleaseDetail State { get; init; }

    /// <summary>The same findings as <see cref="Report"/>.</summary>
    public required IReadOnlyList<ReleaseViolation> Violations { get; init; }

    /// <summary>The release rules over the pending manifest; <c>unsaved</c> is true.</summary>
    public required ReleaseCheckReport Report { get; init; }

    /// <summary>The pending manifest compared with the active release's; null when no release is active.</summary>
    public required ReleaseDiff? DiffVsActive { get; init; }
}
