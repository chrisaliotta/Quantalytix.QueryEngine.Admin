namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>What checking a version against its publication rules found; the check changes nothing.</summary>
public sealed record VersionCheck
{
    /// <summary>The rule violations; empty when the version passes.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}
