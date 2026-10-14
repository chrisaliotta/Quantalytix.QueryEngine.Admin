namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The complete ordered band list of a draft range dimension version, as <c>PUT dimension-versions/{dimensionVersionId}/range-bands</c> takes it.</summary>
public sealed record RangeBandsInput
{
    /// <summary>The bands in evaluation order; each band's ordinal is its position, starting at 1.</summary>
    public required IReadOnlyList<RangeBandInput> Bands { get; init; }
}
