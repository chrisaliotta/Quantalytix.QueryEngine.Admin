namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>One pin of an assumption set: the exact dimension version it captures for a dimension.</summary>
public sealed record AssumptionPinInput
{
    /// <summary>The canonical dimension.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The version of the dimension that the set pins.</summary>
    public required long DimensionVersionId { get; init; }
}
