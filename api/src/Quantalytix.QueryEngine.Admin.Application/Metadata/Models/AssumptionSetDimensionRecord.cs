namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_assumption_set_dimension</c> row: pins the exact immutable version of one dimension in an assumption set.</summary>
public sealed record AssumptionSetDimensionRecord
{
    /// <summary>The owning assumption set; first part of the primary key.</summary>
    public required long AssumptionSetId { get; init; }

    /// <summary>The canonical dimension; second part of the primary key.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The exact dimension version captured; authoritative, never re-derived.</summary>
    public required long DimensionVersionId { get; init; }
}
