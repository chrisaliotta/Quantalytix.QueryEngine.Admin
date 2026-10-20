namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The editable attributes of a dimension version, as <c>PUT dimension-versions/{dimensionVersionId}</c> takes them.</summary>
public record DimensionVersionInput
{
    /// <summary>A <c>range_data_type</c> value for a range dimension; null for every other kind.</summary>
    public string? RangeDataType { get; init; }
}
