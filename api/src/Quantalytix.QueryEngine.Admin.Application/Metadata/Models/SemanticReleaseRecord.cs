namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_semantic_release</c> row: the immutable manifest root for one coherent structural semantic model.</summary>
public sealed record SemanticReleaseRecord
{
    /// <summary>Surrogate primary key.</summary>
    public required long ReleaseId { get; init; }

    /// <summary>Stable release label.</summary>
    public required string ReleaseKey { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Optional deterministic hash of the release definition graph.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Publication time (UTC); null until published.</summary>
    public DateTime? PublishedAt { get; init; }
}
