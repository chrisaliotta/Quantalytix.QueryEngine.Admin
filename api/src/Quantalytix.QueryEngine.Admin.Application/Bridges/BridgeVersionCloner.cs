using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Copies a bridge version's mappings into a new draft that may pin other endpoint versions. Each mapping follows its source
/// and target member keys to the members with the same keys in the draft's endpoint versions, keeping its factor and
/// weight; a mapping whose source or target key has no such member is dropped and reported.
/// </summary>
public sealed class BridgeVersionCloner(ITableGateway<BridgeMappingRecord> mappings, ITableGateway<DimensionMemberRecord> members)
{
    /// <summary>Copies the source's mappings into the target, remapped by member key.</summary>
    /// <param name="sourceVersionId">The bridge version to copy.</param>
    /// <param name="target">The new draft, already stored.</param>
    /// <param name="cancellationToken">Cancels the reads and the insert.</param>
    /// <returns>The dropped mappings, by source key, then target key.</returns>
    public async Task<IReadOnlyList<DroppedBridgeMapping>> CloneAsync(long sourceVersionId, BridgeVersionRecord target, CancellationToken cancellationToken)
    {
        var sourceMappings = await mappings.ListAsync(new { BridgeVersionId = sourceVersionId }, cancellationToken);
        long[] memberIds = [.. sourceMappings.SelectMany(mapping => new[] { mapping.SourceMemberId, mapping.TargetMemberId }).Distinct()];
        var memberKeys = (await members.ListAsync(new { MemberId = memberIds }, cancellationToken))
            .ToDictionary(member => member.MemberId, member => member.MemberKey);
        var sourceIds = MemberKeys.UniqueIds(await members.ListAsync(new { DimensionVersionId = target.SourceDimensionVersionId }, cancellationToken));
        var targetIds = MemberKeys.UniqueIds(await members.ListAsync(new { DimensionVersionId = target.TargetDimensionVersionId }, cancellationToken));

        var clones = new List<BridgeMappingRecord>();
        var dropped = new List<DroppedBridgeMapping>();
        var ordered = sourceMappings
            .OrderBy(mapping => memberKeys[mapping.SourceMemberId], StringComparer.Ordinal)
            .ThenBy(mapping => memberKeys[mapping.TargetMemberId], StringComparer.Ordinal);
        foreach (var mapping in ordered)
        {
            var sourceKey = memberKeys[mapping.SourceMemberId];
            var targetKey = memberKeys[mapping.TargetMemberId];
            if (sourceIds.TryGetValue(sourceKey, out var sourceMemberId) && targetIds.TryGetValue(targetKey, out var targetMemberId))
                clones.Add(mapping with { BridgeVersionId = target.BridgeVersionId, SourceMemberId = sourceMemberId, TargetMemberId = targetMemberId });
            else
                dropped.Add(new DroppedBridgeMapping { SourceMemberKey = sourceKey, TargetMemberKey = targetKey });
        }

        await mappings.InsertManyAsync(clones, cancellationToken);
        return dropped;
    }
}
