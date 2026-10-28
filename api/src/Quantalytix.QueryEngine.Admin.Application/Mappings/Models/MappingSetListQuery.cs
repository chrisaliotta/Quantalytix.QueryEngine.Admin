namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The query of <c>GET mapping-sets</c>.</summary>
public sealed record MappingSetListQuery
{
    /// <summary>Only the mapping sets that target this dimension; null lists every mapping set.</summary>
    public long? DimensionId { get; init; }
}
