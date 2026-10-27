namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionVersionPatch</c>.</summary>
public sealed record DimensionVersionPatch
{
    /// <summary>The new <c>range_data_type</c>; changing it clears every band bound.</summary>
    public required string? RangeDataType { get; init; }
}
