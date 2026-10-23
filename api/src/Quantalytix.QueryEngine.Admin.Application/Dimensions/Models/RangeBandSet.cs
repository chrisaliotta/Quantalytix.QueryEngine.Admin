using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The range bands of one dimension version as the band rules check them, with the member keys their messages name.</summary>
public sealed record RangeBandSet
{
    /// <summary>The version's <c>range_data_type</c>, which picks the bound family; null when the version lacks one.</summary>
    public string? RangeDataType { get; init; }

    /// <summary>The bands by ordinal; empty when the dimension's kind is not <c>range</c>, because only range dimensions use bands.</summary>
    public required IReadOnlyList<RangeBandRecord> Bands { get; init; }

    /// <summary>The key of each member of the version, by member ID.</summary>
    public required IReadOnlyDictionary<long, string> MemberKeys { get; init; }

    /// <summary>A band as messages name it, such as <c>Band 2 (member 'LOW')</c>; a member outside the version shows its ID.</summary>
    /// <param name="band">One of the set's bands.</param>
    /// <returns>The band's ordinal and member.</returns>
    public string Describe(RangeBandRecord band)
    {
        if (MemberKeys.TryGetValue(band.DimensionMemberId, out var memberKey)) return $"Band {band.Ordinal} (member '{memberKey}')";
        return $"Band {band.Ordinal} (member {band.DimensionMemberId})";
    }
}
