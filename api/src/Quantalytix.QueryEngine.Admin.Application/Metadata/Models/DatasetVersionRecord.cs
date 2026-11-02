namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dataset_version</c> row: one snapshot's physical and logical definition of a dataset.</summary>
public sealed record DatasetVersionRecord
{
    /// <summary>Surrogate primary key.</summary>
    public required long DatasetVersionId { get; init; }

    /// <summary>The owning logical dataset.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The snapshot the version belongs to; unique together with the dataset.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>A <c>source_object_kind</c> value.</summary>
    public required string SourceObjectKind { get; init; }

    /// <summary>Physical table or view name, controlled by metadata rather than request input.</summary>
    public required string SourceObject { get; init; }

    /// <summary>Whether the dataset may satisfy aggregate mode.</summary>
    public required bool SupportsAggregate { get; init; }

    /// <summary>Whether the dataset may satisfy detail mode.</summary>
    public required bool SupportsDetail { get; init; }

    /// <summary>Default time binding when a measure or query does not require another.</summary>
    public long? DefaultTimeBindingId { get; init; }

    /// <summary>Version of the expression grammar the stored field and measure expressions use.</summary>
    public required string ExpressionLanguageVersion { get; init; }

    /// <summary>Optional application authorization policy identifier.</summary>
    public string? SecurityPolicyKey { get; init; }

    /// <summary>Extension metadata as raw JSON text (a JSON column).</summary>
    public string? Properties { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}
