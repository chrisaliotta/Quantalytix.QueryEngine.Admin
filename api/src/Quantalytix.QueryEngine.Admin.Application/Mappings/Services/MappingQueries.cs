using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The mapping set reads: the list of the mapping sets the live snapshot holds, one mapping set, a live or frozen version's content and
/// the delete check.
/// </summary>
public sealed class MappingQueries(
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries,
    MappingReferenceReader references,
    MappingSetSnapshotLoader loader,
    SnapshotRuleRunner<MappingSetSnapshot> rules,
    LiveSnapshot live)
{
    /// <summary>Lists every mapping set, or those that target one dimension.</summary>
    /// <param name="query">The optional dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items by key.</returns>
    public async Task<IReadOnlyList<MappingSetListItem>> ListAsync(MappingSetListQuery query, CancellationToken cancellationToken)
    {
        var liveSnapshotId = await live.IdAsync(cancellationToken);
        var versionsOf = (await versions.ListAsync(null, cancellationToken)).ToLookup(version => version.MappingSetId);
        var listed = (await sets.ListAsync(query.DimensionId is { } dimensionId ? new { DimensionId = dimensionId } : null, cancellationToken))
            .Where(set => versionsOf[set.MappingSetId].Any(version => version.SnapshotId == liveSnapshotId))
            .ToList();
        var setIds = listed.Select(set => set.MappingSetId).ToHashSet();
        var counts = (await entries.ListAsync(new { MappingVersionId = listed.SelectMany(set => versionsOf[set.MappingSetId]).Select(version => version.MappingVersionId).ToArray() }, cancellationToken))
            .GroupBy(entry => entry.MappingVersionId)
            .ToDictionary(group => group.Key, group => group.Count());
        var dimensions = await references.DimensionsAsync(listed.Select(set => set.DimensionId), liveSnapshotId, cancellationToken);
        var bindings = await references.BindingsAsync(
            binding => binding.MappingSetId is { } setId && setIds.Contains(setId),
            binding => binding.MappingSetId!.Value,
            liveSnapshotId,
            cancellationToken);
        return
        [
            .. listed
                .Select(set =>
                {
                    var own = versionsOf[set.MappingSetId].ToList();
                    var entryCounts = own.ToDictionary(version => version.MappingVersionId, version => counts.GetValueOrDefault(version.MappingVersionId));
                    return MappingReadModels.ListItem(set, dimensions[set.DimensionId], own, liveSnapshotId, entryCounts, [.. bindings[set.MappingSetId]]);
                })
                .OrderBy(item => item.Key, StringComparer.Ordinal)
        ];
    }

    /// <summary>Reads one mapping set.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list item.</returns>
    public async Task<MappingSetListItem> DetailAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        return MappingReadModels.Detail(await loader.LoadLiveAsync(mappingSetId, cancellationToken));
    }

    /// <summary>Reads a version's content with the findings of its entries.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content.</returns>
    public async Task<MappingVersionContent> ContentAsync(long versionId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadVersionAsync(versionId, cancellationToken);
        return MappingReadModels.Content(snapshot, await rules.EvaluateAsync(snapshot, cancellationToken), TemporaryIds.PresentStored);
    }

    /// <summary>Checks whether a mapping set can be deleted.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<DeleteCheck> SetDeleteCheckAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        return MappingDeleteRules.Set(await loader.LoadLiveAsync(mappingSetId, cancellationToken));
    }
}
