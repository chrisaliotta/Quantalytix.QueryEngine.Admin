namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The editable attributes of a draft dimension version, as <c>PUT dimension-versions/{dimensionVersionId}</c> takes them.</summary>
public record DimensionVersionInput
{
    /// <summary>A <c>range_data_type</c> value for a range dimension; null for every other kind.</summary>
    public string? RangeDataType { get; init; }

    /// <summary>Inclusive validity start (UTC); a version needs one before it is published.</summary>
    public DateTime? ValidFrom { get; init; }

    /// <summary>Exclusive validity end (UTC), later than <see cref="ValidFrom"/>; null means open-ended.</summary>
    public DateTime? ValidTo { get; init; }
}
