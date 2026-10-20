namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_assumption_set_dimension</c> row: the version of one dimension an assumption set uses.</summary>
public sealed record AssumptionSetDimensionRecord
{
    /// <summary>The owning assumption set; first part of the primary key.</summary>
    public required long AssumptionSetId { get; init; }

    /// <summary>The canonical dimension; second part of the primary key.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The pinned version: published in the active set, published or retired in a historical set.</summary>
    public required long DimensionVersionId { get; init; }
}
