namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_mapping_set</c> row: the stable identity of a source code system that maps onto one canonical dimension.</summary>
public sealed record MappingSetRecord
{
    /// <summary>Primary key.</summary>
    public required long MappingSetId { get; init; }

    /// <summary>Stable mapping-set identity.</summary>
    public required string MappingSetKey { get; init; }

    /// <summary>Namespace identifying the source vocabulary.</summary>
    public required string SourceNamespace { get; init; }

    /// <summary>The canonical target dimension.</summary>
    public required long DimensionId { get; init; }

    /// <summary>Human-readable label.</summary>
    public required string DisplayName { get; init; }
}
