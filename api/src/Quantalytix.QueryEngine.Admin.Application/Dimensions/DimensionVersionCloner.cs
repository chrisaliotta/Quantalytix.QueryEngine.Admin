using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Copies a dimension version's content into a new draft version, remapping every internal ID through one
/// <see cref="IdRemap"/>: levels; members, with their parents and levels; and range bands, with their members. Closure rows
/// are not copied, because validation rebuilds them. Mapping sets and bridges name members by key, so they follow the
/// draft once it is published.
/// </summary>
public sealed class DimensionVersionCloner(
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    IIdGenerator ids)
{
    /// <summary>Copies the source version's content into the target, parents before children.</summary>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="target">The new draft version, already stored.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when every copy is written.</returns>
    public async Task CloneAsync(long sourceVersionId, DimensionVersionRecord target, CancellationToken cancellationToken)
    {
        var remap = new IdRemap(ids);
        var sourceLevels = await levels.ListAsync(new { DimensionVersionId = sourceVersionId }, cancellationToken);
        var sourceMembers = await members.ListAsync(new { DimensionVersionId = sourceVersionId }, cancellationToken);
        var sourceBands = await bands.ListAsync(new { DimensionVersionId = sourceVersionId }, cancellationToken);

        await levels.InsertManyAsync(
            [.. sourceLevels.Select(level => level with { DimensionLevelId = remap.Map(level.DimensionLevelId), DimensionVersionId = target.DimensionVersionId })],
            cancellationToken);
        await members.InsertManyAsync(
            [
                .. sourceMembers.Select(member => member with
                {
                    DimensionMemberId = remap.Map(member.DimensionMemberId),
                    DimensionVersionId = target.DimensionVersionId,
                    ParentDimensionMemberId = remap.MapOptional(member.ParentDimensionMemberId),
                    DimensionLevelId = remap.MapOptional(member.DimensionLevelId)
                })
            ],
            cancellationToken);
        await bands.InsertManyAsync(
            [.. sourceBands.Select(band => band with { DimensionVersionId = target.DimensionVersionId, DimensionMemberId = remap.Map(band.DimensionMemberId) })],
            cancellationToken);
    }
}
