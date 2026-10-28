namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>The contract's <c>DatasetMeasure</c>.</summary>
public sealed record DatasetMeasure : DatasetMeasureDefinition
{
    /// <summary>The measure's id, stored or temporary.</summary>
    public required string Id { get; init; }
}
