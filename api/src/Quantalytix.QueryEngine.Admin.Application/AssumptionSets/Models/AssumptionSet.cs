using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The contract's <c>AssumptionSet</c>: one row of <c>qe_assumption_set</c>.</summary>
public record AssumptionSet
{
    /// <summary>A set as stored.</summary>
    /// <param name="row">The set row.</param>
    [SetsRequiredMembers]
    public AssumptionSet(AssumptionSetRecord row)
    {
        Id = row.AssumptionSetId;
        Key = row.AssumptionSetKey;
        DisplayName = row.DisplayName;
        Description = row.Description;
        Status = row.Status;
        ReleaseId = row.SemanticReleaseId;
        CapturedAt = row.CapturedAt;
        CreatedAt = row.CreatedAt;
    }

    /// <summary>The set.</summary>
    public required long Id { get; init; }

    /// <summary>The key requests name the set by, fixed after creation (R11).</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An optional note.</summary>
    public required string? Description { get; init; }

    /// <summary>An <c>assumption_set_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>The captured release; null for the active set, which reads the active release.</summary>
    public required long? ReleaseId { get; init; }

    /// <summary>When a historical set was captured (UTC); null for the active set.</summary>
    public required DateTime? CapturedAt { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>The contract's <c>SetListItem</c>: a set with its pin counts.</summary>
public sealed record SetListItem : AssumptionSet
{
    /// <summary>A set row with its counts.</summary>
    /// <param name="row">The set row.</param>
    [SetsRequiredMembers]
    public SetListItem(AssumptionSetRecord row)
        : base(row)
    {
    }

    /// <summary>How many identities the set pins.</summary>
    public required int PinCount { get; init; }

    /// <summary>Pins whose analytical version is still published.</summary>
    public required int LivePins { get; init; }

    /// <summary>Pins whose identity now publishes another version; 0 for the active set.</summary>
    public required int ChangedSinceCapture { get; init; }
}

/// <summary>The contract's <c>AssumptionSetList</c>: the active set and the historical sets.</summary>
public sealed record AssumptionSetList
{
    /// <summary>The active set.</summary>
    public required SetListItem Active { get; init; }

    /// <summary>Every historical set, newest capture first.</summary>
    public required IReadOnlyList<SetListItem> Historical { get; init; }
}
