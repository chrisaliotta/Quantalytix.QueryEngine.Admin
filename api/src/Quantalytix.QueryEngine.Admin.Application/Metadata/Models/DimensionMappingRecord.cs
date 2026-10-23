namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dimension_mapping</c> row: maps one actual source value to one canonical member key within one mapping version.</summary>
public sealed record DimensionMappingRecord
{
    /// <summary>Surrogate primary key allocated by the metadata service; no business meaning.</summary>
    public required long DimensionMappingId { get; init; }

    /// <summary>The mapping version that owns the assignment.</summary>
    public required long MappingVersionId { get; init; }

    /// <summary>Actual scalar source value, unique within the mapping version.</summary>
    public required string SourceKeyValue { get; init; }

    /// <summary>The key of the canonical target member, which resolves in the version of the set's dimension an assumption set pins.</summary>
    public required string MemberKey { get; init; }
}
