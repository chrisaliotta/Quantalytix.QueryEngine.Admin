namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The contract's <c>PinIdentity</c>: a dimension, mapping set or bridge that an assumption set pins a version of.</summary>
public sealed record PinIdentity
{
    /// <summary>A <c>PinKind</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The identity.</summary>
    public required long Id { get; init; }

    /// <summary>A dimension key, or a mapping set or bridge dotted key.</summary>
    public required string Key { get; init; }

    /// <summary>The identity's display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dimension itself, a mapping set's dimension, or a bridge's target dimension.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The report that owns that dimension; null for an analytical identity.</summary>
    public required long? ReportId { get; init; }

    /// <summary>When the identity's first version was created (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>The live and frozen versions (OI-14), oldest first.</summary>
    public required IReadOnlyList<PinIdentityVersion> Versions { get; init; }
}

/// <summary>The contract's <c>PinIdentityVersion</c>: a live (published) or frozen (retired) version of a living kind.</summary>
public sealed record PinIdentityVersion
{
    /// <summary>The version.</summary>
    public required long Id { get; init; }

    /// <summary>The version number.</summary>
    public required int VersionNumber { get; init; }

    /// <summary><c>published</c> or <c>retired</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Publication time (UTC).</summary>
    public required DateTime PublishedAt { get; init; }
}
