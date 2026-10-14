namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// A new draft version of a dimension, as <c>POST dimensions/{dimensionId}/versions</c> takes it: empty, or a clone of an
/// existing version. A clone copies the source's range data type unless the input supplies one.
/// </summary>
public sealed record NewDimensionVersionInput : DimensionVersionInput
{
    /// <summary>The version whose levels, members, range bands and mapping versions the new draft copies; null for an empty draft.</summary>
    public long? CloneFromVersionId { get; init; }
}
