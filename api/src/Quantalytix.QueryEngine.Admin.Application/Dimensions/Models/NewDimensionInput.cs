namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// A new user-defined dimension, as <c>POST dimensions</c> takes it: its fixed key and kind, its identity attributes, and the
/// range data type of its draft version 1.
/// </summary>
public sealed record NewDimensionInput : DimensionInput
{
    /// <summary>Stable canonical key, unique across dimensions and fixed once created.</summary>
    public required string DimensionKey { get; init; }

    /// <summary>A <c>dimension_kind</c> value, fixed once created.</summary>
    public required string DimensionKind { get; init; }

    /// <summary>A <c>range_data_type</c> value for version 1; required for a <c>range</c> dimension and null for every other kind.</summary>
    public string? RangeDataType { get; init; }
}
