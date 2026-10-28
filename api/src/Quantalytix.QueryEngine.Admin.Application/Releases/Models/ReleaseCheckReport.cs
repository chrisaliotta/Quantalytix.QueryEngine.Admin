namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ReleaseCheckReport</c>: the release rules' findings over a manifest.</summary>
public sealed record ReleaseCheckReport
{
    /// <summary>The release checked.</summary>
    public required long ReleaseId { get; init; }

    /// <summary>True when the rules ran over staged, unsaved manifest operations (a change-set preview).</summary>
    public required bool Unsaved { get; init; }

    /// <summary>When the rules ran (UTC).</summary>
    public required DateTime CheckedAt { get; init; }

    /// <summary>The findings, in rule order.</summary>
    public required IReadOnlyList<ReleaseViolation> Violations { get; init; }
}
