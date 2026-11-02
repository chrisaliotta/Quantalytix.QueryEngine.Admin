using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Hard-deletes mapping versions, their entries first, and a mapping set once no snapshot holds a version of it.</summary>
public sealed class MappingVersionRemover(
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries) : IVersionRemover
{
    /// <summary><c>mapping_set</c>.</summary>
    public string Kind => SnapshotKinds.MappingSet;

    /// <summary>Deletes a version's entries, then the version.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the remaining statement.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        await entries.DeleteAsync(new { MappingVersionId = versionId }, cancellationToken);
        await versions.DeleteAsync(new { MappingVersionId = versionId }, cancellationToken);
    }

    /// <summary>Deletes a mapping set row.</summary>
    /// <param name="identityId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the row is gone.</returns>
    public async Task RemoveIdentityAsync(long identityId, CancellationToken cancellationToken)
    {
        await sets.DeleteAsync(new { MappingSetId = identityId }, cancellationToken);
    }
}
