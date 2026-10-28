namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>CreateMappingSetInput</c>: a mapping set, created with an empty live version 1 (OI-27).</summary>
public sealed record CreateMappingSetInput
{
    /// <summary>The mapping set key, unique in the tenant and fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free text naming the source system (B14).</summary>
    public required string SourceNamespace { get; init; }

    /// <summary>An analytical categorical or hierarchical dimension (B1), fixed after creation.</summary>
    public required long DimensionId { get; init; }
}
