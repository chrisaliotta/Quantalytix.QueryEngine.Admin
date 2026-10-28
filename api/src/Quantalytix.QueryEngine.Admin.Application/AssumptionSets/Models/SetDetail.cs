using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The contract's <c>SetDetail</c>: a set with its pins and, for a historical set, per-report readiness.</summary>
public sealed record SetDetail
{
    /// <summary>The set.</summary>
    public required AssumptionSet Set { get; init; }

    /// <summary>The release the set reads: its captured one, or the active release for the active set.</summary>
    public required long ReadsReleaseId { get; init; }

    /// <summary>The pins: dimensions, mapping sets, then bridges.</summary>
    public required IReadOnlyList<PinRow> Pins { get; init; }

    /// <summary>One entry per enabled report for a historical set; null for the active set.</summary>
    public required IReadOnlyList<SetReadiness>? Readiness { get; init; }
}

/// <summary>The contract's <c>PinRow</c>: one pin with its identity's published version and its state.</summary>
public sealed record PinRow
{
    /// <summary>A <c>PinKind</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The identity.</summary>
    public required long IdentityId { get; init; }

    /// <summary>The pinned version.</summary>
    public required long VersionId { get; init; }

    /// <summary>The identity's published version now; null when it has none.</summary>
    public required long? PublishedVersionId { get; init; }

    /// <summary>A <c>PinState</c>.</summary>
    public required string State { get; init; }
}

/// <summary>The contract's <c>SetReadiness</c>: whether a report comparison can name the set.</summary>
public sealed record SetReadiness
{
    /// <summary>The enabled report.</summary>
    public required long ReportId { get; init; }

    /// <summary>True when the set pins every analytical identity the report reads and its release pins every source dataset.</summary>
    public required bool Ready { get; init; }

    /// <summary>What is missing; empty when ready.</summary>
    public required IReadOnlyList<SetReadinessReason> Reasons { get; init; }
}

/// <summary>The contract's <c>SetReadinessReason</c>: one thing a set misses for a report.</summary>
public sealed record SetReadinessReason
{
    /// <summary>What is missing.</summary>
    public required string Message { get; init; }

    /// <summary>The identity the set does not pin, or the report source whose dataset the release does not pin.</summary>
    public required ViolationTarget Target { get; init; }
}
