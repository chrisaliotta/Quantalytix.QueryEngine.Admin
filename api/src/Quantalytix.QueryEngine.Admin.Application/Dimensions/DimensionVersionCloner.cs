using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Copies a dimension version's content into a new draft version, remapping every internal ID through one
/// <see cref="IdRemap"/>: levels; members, with their parents and levels; range bands, with their members; and the mapping
/// versions of the dimension's mapping sets that pin the source, each with its entries. Closure rows are not copied,
/// because validation rebuilds them.
/// </summary>
public sealed class DimensionVersionCloner(
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<DimensionMappingRecord> mappingEntries,
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
            [.. sourceLevels.Select(level => level with { LevelId = remap.Map(level.LevelId), DimensionVersionId = target.DimensionVersionId })],
            cancellationToken);
        await members.InsertManyAsync(
            [
                .. sourceMembers.Select(member => member with
                {
                    MemberId = remap.Map(member.MemberId),
                    DimensionVersionId = target.DimensionVersionId,
                    ParentMemberId = remap.MapOptional(member.ParentMemberId),
                    LevelId = remap.MapOptional(member.LevelId)
                })
            ],
            cancellationToken);
        await bands.InsertManyAsync(
            [.. sourceBands.Select(band => band with { DimensionVersionId = target.DimensionVersionId, MemberId = remap.Map(band.MemberId) })],
            cancellationToken);
        await CloneMappingVersionsAsync(sourceVersionId, target, remap, cancellationToken);
    }

    /// <summary>
    /// Gives every mapping set of the dimension a new draft for each of its versions that pins the source, numbered after the
    /// set's existing versions. Retired mapping versions are history, so they are not copied. Each entry follows its member
    /// to that member's clone, which has the same member key; an entry whose member was not cloned is dropped.
    /// </summary>
    private async Task CloneMappingVersionsAsync(long sourceVersionId, DimensionVersionRecord target, IdRemap remap, CancellationToken cancellationToken)
    {
        var sets = await mappingSets.ListAsync(new { target.DimensionId }, cancellationToken);
        var setVersions = await mappingVersions.ListAsync(
            new { MappingSetId = sets.Select(set => set.MappingSetId).ToArray() },
            cancellationToken);
        var sources = setVersions
            .Where(version => version.DimensionVersionId == sourceVersionId && version.Status != PublicationStatuses.Retired)
            .ToList();
        var entries = await mappingEntries.ListAsync(
            new { MappingVersionId = sources.Select(version => version.MappingVersionId).ToArray() },
            cancellationToken);

        var clones = new List<MappingVersionRecord>();
        foreach (var setSources in sources.GroupBy(version => version.MappingSetId))
        {
            var number = VersionNumbers.Next(setVersions.Where(version => version.MappingSetId == setSources.Key).Select(version => version.VersionNumber));
            foreach (var source in setSources.OrderBy(version => version.VersionNumber))
            {
                clones.Add(source with
                {
                    MappingVersionId = remap.Map(source.MappingVersionId),
                    VersionNumber = number++,
                    DimensionVersionId = target.DimensionVersionId,
                    Status = PublicationStatuses.Draft,
                    PublishedAt = null
                });
            }
        }

        await mappingVersions.InsertManyAsync(clones, cancellationToken);
        await mappingEntries.InsertManyAsync(
            [
                .. entries.Where(entry => remap.Contains(entry.MemberId)).Select(entry => entry with
                {
                    MappingEntryId = remap.Map(entry.MappingEntryId),
                    MappingVersionId = remap.Map(entry.MappingVersionId),
                    MemberId = remap.Map(entry.MemberId)
                })
            ],
            cancellationToken);
    }
}
