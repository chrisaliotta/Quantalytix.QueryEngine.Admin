using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Hard-deletes mapping versions, their entries first, and mapping sets once no snapshot holds a version of them.</summary>
public sealed class MappingVersionRemover(
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries) : IVersionRemover
{
    /// <summary><c>mapping_set</c>.</summary>
    public string Kind => SnapshotKinds.MappingSet;

    /// <summary>Deletes the versions' entries, then the versions.</summary>
    /// <param name="versionIds">The mapping versions.</param>
    /// <param name="cancellationToken">Cancels the remaining statement.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveVersionsAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await entries.DeleteAsync(new { MappingVersionId = versionIds }, cancellationToken);
        await versions.DeleteAsync(new { MappingVersionId = versionIds }, cancellationToken);
    }

    /// <summary>Deletes mapping set rows.</summary>
    /// <param name="identityIds">The mapping sets.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveIdentitiesAsync(IReadOnlyCollection<long> identityIds, CancellationToken cancellationToken)
    {
        await sets.DeleteAsync(new { MappingSetId = identityIds }, cancellationToken);
    }
}
