using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetCreateDraftInput</c> union, discriminated by <c>mode</c>: an empty draft or a clone.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "mode")]
[JsonDerivedType(typeof(DatasetCreateEmptyDraftInput), "empty")]
[JsonDerivedType(typeof(DatasetCloneDraftInput), "clone")]
public abstract record DatasetCreateDraftInput;

/// <summary><c>empty</c>: a draft without content over a table or view.</summary>
public sealed record DatasetCreateEmptyDraftInput : DatasetCreateDraftInput
{
    /// <summary>A <c>source_object_kind</c> value.</summary>
    public required string SourceObjectKind { get; init; }

    /// <summary>The table or view.</summary>
    public required string SourceObject { get; init; }
}

/// <summary><c>clone</c>: a draft copying another version of the same dataset.</summary>
public sealed record DatasetCloneDraftInput : DatasetCreateDraftInput
{
    /// <summary>The version to copy.</summary>
    public required long FromVersionId { get; init; }
}
