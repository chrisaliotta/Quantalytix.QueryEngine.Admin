using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The entries of one mapping version as the entry rules check them, with the keys of the dimension version they resolve in.</summary>
public sealed record MappingEntrySet
{
    /// <summary>The entries, by source key in ordinal order.</summary>
    public required IReadOnlyList<DimensionMappingRecord> Entries { get; init; }

    /// <summary>The keys of the published version of the set's dimension; null while it has none.</summary>
    public required DimensionKeys? Dimension { get; init; }
}
