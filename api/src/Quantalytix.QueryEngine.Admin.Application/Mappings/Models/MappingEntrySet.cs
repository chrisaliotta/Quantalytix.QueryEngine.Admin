using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The entries of one mapping version as the entry rules check them, with the members of its pinned dimension version.</summary>
public sealed record MappingEntrySet
{
    /// <summary>The entries, by source key in ordinal order.</summary>
    public required IReadOnlyList<DimensionMappingRecord> Entries { get; init; }

    /// <summary>The members of the dimension version the mapping version pins, by member ID.</summary>
    public required IReadOnlyDictionary<long, DimensionMemberRecord> Members { get; init; }
}
