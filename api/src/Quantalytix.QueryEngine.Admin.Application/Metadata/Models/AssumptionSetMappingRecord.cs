namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_assumption_set_mapping</c> row: the version of one mapping set an assumption set uses.</summary>
public sealed record AssumptionSetMappingRecord
{
    /// <summary>The owning assumption set; first part of the primary key.</summary>
    public required long AssumptionSetId { get; init; }

    /// <summary>The mapping set; second part of the primary key.</summary>
    public required long MappingSetId { get; init; }

    /// <summary>The pinned version: published in the active set, published or retired in a historical set.</summary>
    public required long MappingVersionId { get; init; }
}
