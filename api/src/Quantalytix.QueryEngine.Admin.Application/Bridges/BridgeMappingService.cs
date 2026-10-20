using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Reads and replaces the mappings of a bridge version: its source-member to target-member assignments.</summary>
public sealed class BridgeMappingService(ITableGateway<BridgeMappingRecord> mappings, IIdGenerator ids)
{
    /// <summary>Lists a version's mappings.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The mappings by source member key, then target member key.</returns>
    public async Task<IReadOnlyList<BridgeMappingRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        return ByMembers(await mappings.ListAsync(new { BridgeVersionId = versionId }, cancellationToken));
    }

    /// <summary>Replaces every mapping of a version with the given list, under new mapping IDs.</summary>
    /// <param name="versionId">The draft or published bridge version.</param>
    /// <param name="input">The complete mapping list.</param>
    /// <param name="cancellationToken">Cancels the delete and the insert.</param>
    /// <returns>The stored mappings by source member key, then target member key.</returns>
    public async Task<IReadOnlyList<BridgeMappingRecord>> ReplaceAsync(long versionId, BridgeMappingsInput input, CancellationToken cancellationToken)
    {
        List<BridgeMappingRecord> rows =
        [
            .. input.Mappings.Select(mapping => new BridgeMappingRecord
            {
                BridgeMappingId = ids.NextId(),
                BridgeVersionId = versionId,
                SourceMemberKey = mapping.SourceMemberKey,
                TargetMemberKey = mapping.TargetMemberKey,
                Factor = mapping.Factor,
                AllocationWeight = mapping.AllocationWeight
            })
        ];
        await mappings.DeleteAsync(new { BridgeVersionId = versionId }, cancellationToken);
        await mappings.InsertManyAsync(rows, cancellationToken);
        return ByMembers(rows);
    }

    /// <summary>Orders mappings by source member key, then target member key, compared exactly.</summary>
    private static IReadOnlyList<BridgeMappingRecord> ByMembers(IEnumerable<BridgeMappingRecord> rows)
    {
        return [.. rows.OrderBy(row => row.SourceMemberKey, StringComparer.Ordinal).ThenBy(row => row.TargetMemberKey, StringComparer.Ordinal)];
    }
}
