using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Copies dimension versions' content into other stored versions, remapping every internal ID through one
/// <see cref="IdRemap"/>: levels; members, with their parents and levels; range bands, with their members; and the closure, derived
/// from the copied members. Mapping sets and bridges name members by key, so they follow the copy. Each table is written with one
/// statement for every version copied.
/// </summary>
public sealed class DimensionVersionCloner(
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    ITableGateway<DimensionClosureRecord> closure,
    IIdGenerator ids)
{
    /// <summary>Copies one version's content into another, parents before children.</summary>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="targetVersionId">The stored version that receives the copy, without content.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when every copy is written.</returns>
    public Task CloneAsync(long sourceVersionId, long targetVersionId, CancellationToken cancellationToken)
    {
        return CloneAsync(new Dictionary<long, long> { [sourceVersionId] = targetVersionId }, cancellationToken);
    }

    /// <summary>Copies each source version's content into its target, parents before children.</summary>
    /// <param name="targets">The stored version, without content, that receives each source version's copy.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when every copy is written.</returns>
    public async Task CloneAsync(IReadOnlyDictionary<long, long> targets, CancellationToken cancellationToken)
    {
        var remap = new IdRemap(ids);
        var sources = new { DimensionVersionId = targets.Keys.ToArray() };
        var sourceLevels = await levels.ListAsync(sources, cancellationToken);
        var sourceMembers = await members.ListAsync(sources, cancellationToken);
        var sourceBands = await bands.ListAsync(sources, cancellationToken);
        List<DimensionMemberRecord> copiedMembers =
        [
            .. sourceMembers.Select(member => member with
            {
                DimensionMemberId = remap.Map(member.DimensionMemberId),
                DimensionVersionId = targets[member.DimensionVersionId],
                ParentDimensionMemberId = remap.MapOptional(member.ParentDimensionMemberId),
                DimensionLevelId = remap.MapOptional(member.DimensionLevelId)
            })
        ];

        await levels.InsertManyAsync(
            [.. sourceLevels.Select(level => level with { DimensionLevelId = remap.Map(level.DimensionLevelId), DimensionVersionId = targets[level.DimensionVersionId] })],
            cancellationToken);
        await members.InsertManyAsync(copiedMembers, cancellationToken);
        await bands.InsertManyAsync(
            [.. sourceBands.Select(band => band with { DimensionVersionId = targets[band.DimensionVersionId], DimensionMemberId = remap.Map(band.DimensionMemberId) })],
            cancellationToken);
        await closure.InsertManyAsync(
            [.. copiedMembers.GroupBy(member => member.DimensionVersionId).SelectMany(tree => ClosureBuilder.ClosureOf(tree.Key, [.. tree]))],
            cancellationToken);
    }
}
