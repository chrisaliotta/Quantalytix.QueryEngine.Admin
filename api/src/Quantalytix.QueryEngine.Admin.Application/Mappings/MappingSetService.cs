using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Reads, creates, edits and deletes mapping sets: the source code systems that map onto canonical dimensions.</summary>
public sealed class MappingSetService(
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionBindingRecord> bindings,
    MappingVersionService versionService,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>Lists every mapping set.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The mapping sets by key, ignoring case.</returns>
    public async Task<IReadOnlyList<MappingSetRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await mappingSets.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.MappingSetKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one mapping set.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The mapping set.</returns>
    public Task<MappingSetRecord> GetAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        return mappingSets.GetAsync(new { MappingSetId = mappingSetId }, cancellationToken);
    }

    /// <summary>Creates a mapping set without versions; its entries belong to versions, so none exists yet.</summary>
    /// <param name="input">The new mapping set, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored mapping set.</returns>
    public async Task<MappingSetRecord> CreateAsync(NewMappingSetInput input, CancellationToken cancellationToken)
    {
        var mappingSet = new MappingSetRecord
        {
            MappingSetId = ids.NextId(),
            MappingSetKey = input.MappingSetKey,
            SourceNamespace = input.SourceNamespace,
            DimensionId = input.DimensionId,
            DisplayName = input.DisplayName
        };
        await mappingSets.InsertAsync(mappingSet, cancellationToken);
        return mappingSet;
    }

    /// <summary>
    /// Edits a mapping set's source namespace and display name. A change advances the semantic revision, because the query
    /// engine caches mapping sets with their namespace.
    /// </summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The stored mapping set.</returns>
    public async Task<MappingSetRecord> UpdateAsync(long mappingSetId, MappingSetInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(mappingSetId, cancellationToken);
        var updated = current with { SourceNamespace = input.SourceNamespace, DisplayName = input.DisplayName };
        await mappingSets.UpdateAsync(updated, cancellationToken);
        if (updated != current) await semanticRevision.AdvanceAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a mapping set whose versions are all drafts and that no binding references: its drafts with their entries
    /// first, then the set.
    /// </summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the set was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        var mappingSet = await GetAsync(mappingSetId, cancellationToken);
        var setVersions = await versions.ListAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        var blockers = new DeleteBlockers($"Mapping set '{mappingSet.MappingSetKey}'");
        blockers.AddNamed(
            "it has non-draft versions",
            setVersions.Where(version => version.Status != PublicationStatuses.Draft).Select(version => $"v{version.VersionNumber} ({version.Status})"));
        var boundBy = await bindings.ListAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        blockers.AddNamed("referenced by bindings", boundBy.Select(binding => $"'{binding.DisplayName}'"));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        await versionService.DeleteAsync([.. setVersions.Select(version => version.MappingVersionId)], cancellationToken);
        await mappingSets.DeleteAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        return null;
    }
}
