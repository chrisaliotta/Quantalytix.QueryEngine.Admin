namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dimension_mapping</c> row: maps one actual source value to exactly one canonical member within one mapping version.</summary>
public sealed record DimensionMappingRecord
{
    /// <summary>Surrogate primary key allocated by the metadata service; no business meaning.</summary>
    public required long MappingEntryId { get; init; }

    /// <summary>The mapping version that owns the assignment.</summary>
    public required long MappingVersionId { get; init; }

    /// <summary>Actual scalar source value, unique within the mapping version.</summary>
    public required string SourceKeyValue { get; init; }

    /// <summary>The canonical target member, from the mapping version's dimension version.</summary>
    public required long MemberId { get; init; }
}
