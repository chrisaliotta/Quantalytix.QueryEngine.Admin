using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// The answer of <c>GET &lt;resource&gt;/delete-check</c> (blueprint 01, OI-07); <c>DELETE &lt;resource&gt;</c> answers 409 with the
/// same reasons when the check does not allow it. Built by <see cref="DeleteBlockers.Check"/>.
/// </summary>
public sealed record DeleteCheck
{
    /// <summary>Whether nothing blocks the delete.</summary>
    public required bool Allowed { get; init; }

    /// <summary>One sentence naming every reason, such as <c>Dimension 'Purpose' cannot be deleted: ...</c>; null when allowed.</summary>
    public required string? Message { get; init; }

    /// <summary>The blockers, each coded with one of <see cref="DeleteBlockerCodes"/>; empty when allowed.</summary>
    public required IReadOnlyList<RuleViolation> Reasons { get; init; }
}
