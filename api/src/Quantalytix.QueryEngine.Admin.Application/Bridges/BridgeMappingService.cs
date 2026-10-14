using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Reads and replaces the mappings of a bridge version: its source-member to target-member assignments.</summary>
public sealed class BridgeMappingService(ITableGateway<BridgeMappingRecord> mappings)
{
    /// <summary>Lists a version's mappings.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The mappings by source member ID, then target member ID.</returns>
    public async Task<IReadOnlyList<BridgeMappingRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        return ByMembers(await mappings.ListAsync(new { BridgeVersionId = versionId }, cancellationToken));
    }

    /// <summary>Replaces every mapping of a draft version with the given list.</summary>
    /// <param name="versionId">The draft bridge version.</param>
    /// <param name="input">The complete mapping list.</param>
    /// <param name="cancellationToken">Cancels the delete and the insert.</param>
    /// <returns>The stored mappings by source member ID, then target member ID.</returns>
    public async Task<IReadOnlyList<BridgeMappingRecord>> ReplaceAsync(long versionId, BridgeMappingsInput input, CancellationToken cancellationToken)
    {
        List<BridgeMappingRecord> rows =
        [
            .. input.Mappings.Select(mapping => new BridgeMappingRecord
            {
                BridgeVersionId = versionId,
                SourceMemberId = mapping.SourceMemberId,
                TargetMemberId = mapping.TargetMemberId,
                Factor = mapping.Factor,
                AllocationWeight = mapping.AllocationWeight
            })
        ];
        await mappings.DeleteAsync(new { BridgeVersionId = versionId }, cancellationToken);
        await mappings.InsertManyAsync(rows, cancellationToken);
        return ByMembers(rows);
    }

    /// <summary>Orders mappings by source member ID, then target member ID.</summary>
    private static IReadOnlyList<BridgeMappingRecord> ByMembers(IEnumerable<BridgeMappingRecord> rows)
    {
        return [.. rows.OrderBy(row => row.SourceMemberId).ThenBy(row => row.TargetMemberId)];
    }
}
