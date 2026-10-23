namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dataset_grain</c> row: how rows or temporal series inside one immutable dataset version are identified.</summary>
public sealed record DatasetGrainRecord
{
    /// <summary>Surrogate primary key.</summary>
    public required long DatasetGrainId { get; init; }

    /// <summary>The owning dataset version.</summary>
    public required long DatasetVersionId { get; init; }

    /// <summary>Stable grain name, unique within the dataset version, such as <c>rowIdentity</c>.</summary>
    public required string GrainKey { get; init; }

    /// <summary>A <c>grain_kind</c> value.</summary>
    public required string GrainKind { get; init; }
}
