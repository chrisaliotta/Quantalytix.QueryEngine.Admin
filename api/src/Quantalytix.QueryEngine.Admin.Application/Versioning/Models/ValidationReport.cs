namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>What running a version's rules found (the contract's <c>ValidationReport</c>); the run itself writes nothing.</summary>
public sealed record ValidationReport
{
    /// <summary>The version the rules ran on.</summary>
    public required long VersionId { get; init; }

    /// <summary>When the rules ran (UTC).</summary>
    public required DateTime CheckedAt { get; init; }

    /// <summary>The violations; empty when the version passes.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}
