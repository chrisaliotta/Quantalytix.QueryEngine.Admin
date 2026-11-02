using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Copies a dimension version's content into another stored version, remapping every internal ID through one
/// <see cref="IdRemap"/>: levels; members, with their parents and levels; range bands, with their members; and the closure, derived
/// from the copied members. Mapping sets and bridges name members by key, so they follow the copy.
/// </summary>
public sealed class DimensionVersionCloner(
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    ITableGateway<DimensionClosureRecord> closure,
    IIdGenerator ids)
{
    /// <summary>Copies the source version's content into the target, parents before children.</summary>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="targetVersionId">The stored version that receives the copy, without content.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when every copy is written.</returns>
    public async Task CloneAsync(long sourceVersionId, long targetVersionId, CancellationToken cancellationToken)
    {
        var remap = new IdRemap(ids);
        var sourceLevels = await levels.ListAsync(new { DimensionVersionId = sourceVersionId }, cancellationToken);
        var sourceMembers = await members.ListAsync(new { DimensionVersionId = sourceVersionId }, cancellationToken);
        var sourceBands = await bands.ListAsync(new { DimensionVersionId = sourceVersionId }, cancellationToken);
        List<DimensionMemberRecord> copiedMembers =
        [
            .. sourceMembers.Select(member => member with
            {
                DimensionMemberId = remap.Map(member.DimensionMemberId),
                DimensionVersionId = targetVersionId,
                ParentDimensionMemberId = remap.MapOptional(member.ParentDimensionMemberId),
                DimensionLevelId = remap.MapOptional(member.DimensionLevelId)
            })
        ];

        await levels.InsertManyAsync(
            [.. sourceLevels.Select(level => level with { DimensionLevelId = remap.Map(level.DimensionLevelId), DimensionVersionId = targetVersionId })],
            cancellationToken);
        await members.InsertManyAsync(copiedMembers, cancellationToken);
        await bands.InsertManyAsync(
            [.. sourceBands.Select(band => band with { DimensionVersionId = targetVersionId, DimensionMemberId = remap.Map(band.DimensionMemberId) })],
            cancellationToken);
        await closure.InsertManyAsync(ClosureBuilder.ClosureOf(targetVersionId, copiedMembers), cancellationToken);
    }
}
