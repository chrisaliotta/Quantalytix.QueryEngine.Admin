using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Hard-deletes mapping versions nothing references, their entries first.</summary>
public sealed class MappingVersionRemover(ITableGateway<MappingVersionRecord> versions, ITableGateway<DimensionMappingRecord> entries)
{
    /// <summary>Deletes the versions and their entries, one statement per table.</summary>
    /// <param name="versionIds">The mapping versions.</param>
    /// <param name="cancellationToken">Cancels the remaining statement.</param>
    /// <returns>A task that completes when every row is deleted.</returns>
    public async Task RemoveAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await entries.DeleteAsync(new { MappingVersionId = versionIds }, cancellationToken);
        await versions.DeleteAsync(new { MappingVersionId = versionIds }, cancellationToken);
    }
}
