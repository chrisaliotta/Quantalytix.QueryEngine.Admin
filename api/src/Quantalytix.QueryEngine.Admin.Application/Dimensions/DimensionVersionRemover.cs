using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Hard-deletes draft dimension versions with everything they own, children before parents: the entries of the draft
/// mapping versions pinned to them, those mapping versions, range bands, closure rows, members, levels, then the versions.
/// Callers check first that nothing else references the versions.
/// </summary>
public sealed class DimensionVersionRemover(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<DimensionClosureRecord> closure,
    ITableGateway<RangeBandRecord> bands,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<DimensionMappingRecord> mappingEntries)
{
    /// <summary>Deletes the versions and their content, one statement per table.</summary>
    /// <param name="versionIds">The draft dimension versions.</param>
    /// <param name="cancellationToken">Cancels the remaining statements.</param>
    /// <returns>A task that completes when every row is deleted.</returns>
    public async Task RemoveAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        var draftMappings = await mappingVersions.ListAsync(
            new { DimensionVersionId = versionIds, Status = PublicationStatuses.Draft },
            cancellationToken);
        long[] mappingVersionIds = [.. draftMappings.Select(mapping => mapping.MappingVersionId)];
        await mappingEntries.DeleteAsync(new { MappingVersionId = mappingVersionIds }, cancellationToken);
        await mappingVersions.DeleteAsync(new { MappingVersionId = mappingVersionIds }, cancellationToken);
        await bands.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await closure.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await members.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await levels.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await versions.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
    }
}
