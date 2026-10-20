namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of one versioned object.</summary>
public sealed record VersionState
{
    /// <summary>A <c>version_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>The version's surrogate key.</summary>
    public required long Id { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>When the version was published (UTC); null until then.</summary>
    public DateTime? PublishedAt { get; init; }
}
