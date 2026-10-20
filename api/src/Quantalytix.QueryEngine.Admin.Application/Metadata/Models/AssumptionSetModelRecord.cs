namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_assumption_set_model</c> row: the version of one calculation model an assumption set uses.</summary>
public sealed record AssumptionSetModelRecord
{
    /// <summary>The owning assumption set; first part of the primary key.</summary>
    public required long AssumptionSetId { get; init; }

    /// <summary>The calculation model; second part of the primary key.</summary>
    public required long ModelId { get; init; }

    /// <summary>The pinned version: published in the active set, published or retired in a historical set.</summary>
    public required long ModelVersionId { get; init; }
}
