using Quantalytix.QueryEngine.Admin.Application.Releases;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The contract's <c>SetComparison</c>: what <c>to</c> pins differently from <c>from</c>, report-owned identities left out.</summary>
public sealed record SetComparison
{
    /// <summary>The set compared from.</summary>
    public required long FromSetId { get; init; }

    /// <summary>The set compared to.</summary>
    public required long ToSetId { get; init; }

    /// <summary>Identities only <c>to</c> pins.</summary>
    public required IReadOnlyList<SetPinEntry> Added { get; init; }

    /// <summary>Identities only <c>from</c> pins.</summary>
    public required IReadOnlyList<SetPinEntry> Removed { get; init; }

    /// <summary>Identities both pin at different versions.</summary>
    public required IReadOnlyList<SetPinChange> Changed { get; init; }

    /// <summary>Identities pinned at the same version in both.</summary>
    public required int Unchanged { get; init; }

    /// <summary>The difference of the two sets' releases; null when both read the same release.</summary>
    public required ReleaseDiff? Release { get; init; }
}

/// <summary>The contract's <c>SetPinEntry</c>: an identity and the version one side pins.</summary>
public sealed record SetPinEntry
{
    /// <summary>A <c>PinKind</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The identity.</summary>
    public required long IdentityId { get; init; }

    /// <summary>The pinned version.</summary>
    public required long VersionId { get; init; }
}

/// <summary>The contract's <c>SetPinChange</c>: an identity pinned at another version.</summary>
public sealed record SetPinChange
{
    /// <summary>A <c>PinKind</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The identity.</summary>
    public required long IdentityId { get; init; }

    /// <summary>The version <c>from</c> pins.</summary>
    public required long FromVersionId { get; init; }

    /// <summary>The version <c>to</c> pins.</summary>
    public required long ToVersionId { get; init; }
}
