using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Reads what the bridge mapping rules check: a bridge version, its mappings and the member keys of its endpoint versions.</summary>
public sealed class BridgeMappingSetReader(
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    ITableGateway<DimensionMemberRecord> members)
{
    /// <summary>Reads a bridge version's mappings with its endpoint members.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The version's mapping set.</returns>
    public async Task<BridgeMappingSet> ReadAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { BridgeVersionId = versionId }, cancellationToken);
        var rows = await mappings.ListAsync(new { BridgeVersionId = versionId }, cancellationToken);
        var sourceMembers = await members.ListAsync(new { DimensionVersionId = version.SourceDimensionVersionId }, cancellationToken);
        var targetMembers = await members.ListAsync(new { DimensionVersionId = version.TargetDimensionVersionId }, cancellationToken);
        var set = new BridgeMappingSet
        {
            Version = version,
            Mappings = [],
            SourceMemberKeys = sourceMembers.ToDictionary(member => member.MemberId, member => member.MemberKey),
            TargetMemberKeys = targetMembers.ToDictionary(member => member.MemberId, member => member.MemberKey)
        };
        return set with
        {
            Mappings =
            [
                .. rows
                    .OrderBy(row => set.DescribeSource(row.SourceMemberId), StringComparer.Ordinal)
                    .ThenBy(row => set.DescribeTarget(row.TargetMemberId), StringComparer.Ordinal)
            ]
        };
    }
}
