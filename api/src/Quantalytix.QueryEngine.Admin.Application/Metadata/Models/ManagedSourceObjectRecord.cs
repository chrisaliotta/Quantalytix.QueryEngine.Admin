namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_managed_source_object</c> row: a physical table the API created for a dataset and may drop again.</summary>
public sealed record ManagedSourceObjectRecord
{
    /// <summary>Surrogate primary key.</summary>
    public required long ManagedSourceObjectId { get; init; }

    /// <summary>The physical table or view name.</summary>
    public required string SourceObject { get; init; }

    /// <summary>A <c>source_object_kind</c> value.</summary>
    public required string SourceObjectKind { get; init; }

    /// <summary>The dataset whose authoring created the object.</summary>
    public required long CreatedByDatasetId { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}
