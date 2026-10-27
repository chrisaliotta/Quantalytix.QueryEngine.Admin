namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_release_activation</c> row: one activation of a semantic release, the tenant's activation log.</summary>
public sealed record ReleaseActivationRecord
{
    /// <summary>Surrogate primary key.</summary>
    public required long ReleaseActivationId { get; init; }

    /// <summary>The release activated.</summary>
    public required long SemanticReleaseId { get; init; }

    /// <summary>The release that was active before; null for the tenant's first activation.</summary>
    public long? PreviousSemanticReleaseId { get; init; }

    /// <summary>Activation time (UTC).</summary>
    public required DateTime ActivatedAt { get; init; }

    /// <summary>The subject of the caller who activated the release.</summary>
    public required string ActivatedBy { get; init; }
}
