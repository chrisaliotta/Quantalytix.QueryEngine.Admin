namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The query of <c>GET dimensions</c>.</summary>
public sealed record DimensionListQuery
{
    /// <summary>A <c>definition_origin</c> value: the dimensions to list.</summary>
    public required string Origin { get; init; }
}
