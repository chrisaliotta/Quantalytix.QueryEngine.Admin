using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The mapping set reads: the list, the detail, version content, rule checks and delete checks.</summary>
public sealed class MappingQueries(
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries,
    MappingReferenceReader references,
    MappingSetSnapshotLoader loader,
    SnapshotRuleRunner<MappingSetSnapshot> rules,
    TimeProvider time)
{
    /// <summary>Lists every mapping set, or those that target one dimension.</summary>
    /// <param name="query">The optional dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items by key.</returns>
    public async Task<IReadOnlyList<MappingSetListItem>> ListAsync(MappingSetListQuery query, CancellationToken cancellationToken)
    {
        var listed = await sets.ListAsync(query.DimensionId is { } dimensionId ? new { DimensionId = dimensionId } : null, cancellationToken);
        long[] setIds = [.. listed.Select(set => set.MappingSetId)];
        var versionsOf = (await versions.ListAsync(new { MappingSetId = setIds }, cancellationToken)).ToLookup(version => version.MappingSetId);
        var counts = (await entries.ListAsync(new { MappingVersionId = versionsOf.SelectMany(group => group).Select(version => version.MappingVersionId).ToArray() }, cancellationToken))
            .GroupBy(entry => entry.MappingVersionId)
            .ToDictionary(group => group.Key, group => group.Count());
        var dimensions = await references.DimensionsAsync(listed.Select(set => set.DimensionId), cancellationToken);
        var bindings = await references.BindingsAsync(new { MappingSetId = setIds }, binding => binding.MappingSetId!.Value, cancellationToken);
        return
        [
            .. listed
                .Select(set =>
                {
                    var own = versionsOf[set.MappingSetId].ToList();
                    var entryCounts = own.ToDictionary(version => version.MappingVersionId, version => counts.GetValueOrDefault(version.MappingVersionId));
                    return MappingReadModels.ListItem(set, dimensions[set.DimensionId], own, entryCounts, [.. bindings[set.MappingSetId]]);
                })
                .OrderBy(item => item.Key, StringComparer.Ordinal)
        ];
    }

    /// <summary>Reads a mapping set's detail.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The detail.</returns>
    public async Task<MappingSetDetail> DetailAsync(long mappingSetId, CancellationToken cancellationToken)
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

    /// <summary>Runs every mapping rule on a version and changes nothing (OI-30).</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report.</returns>
    public async Task<ValidationReport> CheckAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = await rules.EvaluateAsync(await loader.LoadVersionAsync(versionId, cancellationToken), cancellationToken);
        return new ValidationReport { VersionId = versionId, CheckedAt = time.GetUtcNow().UtcDateTime, Violations = violations };
    }

    /// <summary>Checks whether a mapping set can be deleted.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<DeleteCheck> SetDeleteCheckAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        return MappingDeleteRules.Set(await loader.LoadLiveAsync(mappingSetId, cancellationToken));
    }

    /// <summary>Checks whether a version can be deleted.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<DeleteCheck> VersionDeleteCheckAsync(long versionId, CancellationToken cancellationToken)
    {
        return MappingDeleteRules.Version(await loader.LoadVersionAsync(versionId, cancellationToken));
    }
}
