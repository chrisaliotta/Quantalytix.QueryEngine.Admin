namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetVersionSummary</c>: one version and the snapshot it belongs to.</summary>
public record DatasetVersionSummary
{
    /// <summary>The version's id.</summary>
    public required string Id { get; init; }

    /// <summary>The dataset's id.</summary>
    public required string DatasetId { get; init; }

    /// <summary>The snapshot the version belongs to.</summary>
    public required string SnapshotId { get; init; }

    /// <summary>Whether the version is live, and so editable; a frozen version is read-only.</summary>
    public required bool IsLive { get; init; }

    /// <summary>The table or view the version reads.</summary>
    public required string SourceObject { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}
