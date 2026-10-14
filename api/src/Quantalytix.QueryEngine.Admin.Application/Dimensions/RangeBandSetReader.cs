using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Reads what the range band rules check: a version's bands by ordinal, its range data type and its member keys.</summary>
public sealed class RangeBandSetReader(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<RangeBandRecord> bands,
    ITableGateway<DimensionMemberRecord> members)
{
    /// <summary>Reads the bands of a version; a dimension whose kind is not <c>range</c> has none to check.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The version's band set.</returns>
    public async Task<RangeBandSet> ReadAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var dimension = await dimensions.GetAsync(new { version.DimensionId }, cancellationToken);
        if (dimension.DimensionKind != DimensionKinds.Range)
            return new RangeBandSet { RangeDataType = version.RangeDataType, Bands = [], MemberKeys = new Dictionary<long, string>() };

        var rows = await bands.ListAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var versionMembers = await members.ListAsync(new { DimensionVersionId = versionId }, cancellationToken);
        return new RangeBandSet
        {
            RangeDataType = version.RangeDataType,
            Bands = [.. rows.OrderBy(row => row.Ordinal)],
            MemberKeys = versionMembers.ToDictionary(member => member.MemberId, member => member.MemberKey)
        };
    }
}
