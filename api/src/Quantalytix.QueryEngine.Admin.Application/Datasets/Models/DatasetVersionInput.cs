namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>A new draft version of a dataset: a clone of an existing version, or an empty version over a source object.</summary>
public sealed record DatasetVersionInput
{
    /// <summary>A version of the same dataset whose settings and content the new draft copies; null for an empty draft.</summary>
    public long? CloneFromVersionId { get; init; }

    /// <summary>The <c>source_object_kind</c> of an empty draft's source object; null for a clone.</summary>
    public string? SourceObjectKind { get; init; }

    /// <summary>The table or view an empty draft queries; null for a clone.</summary>
    public string? SourceObject { get; init; }
}
