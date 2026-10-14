namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dataset_grain_field</c> row: one ordered field component of a dataset grain.</summary>
public sealed record DatasetGrainFieldRecord
{
    /// <summary>The owning grain; first part of the primary key.</summary>
    public required long GrainId { get; init; }

    /// <summary>Component order within the grain; second part of the primary key.</summary>
    public required int Ordinal { get; init; }

    /// <summary>The field taking part in the grain, from the grain's dataset version.</summary>
    public required long FieldId { get; init; }
}
