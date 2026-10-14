using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Reads, creates, clones and deletes the versions of mapping sets. Status changes go through the version workflow.</summary>
public sealed class MappingVersionService(
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries,
    MappingVersionCloner cloner,
    IIdGenerator ids)
{
    /// <summary>Lists a mapping set's versions.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The versions by version number.</returns>
    public async Task<IReadOnlyList<MappingVersionRecord>> ListAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        var rows = await versions.ListAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        return [.. rows.OrderBy(row => row.VersionNumber)];
    }

    /// <summary>Reads one mapping version.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version.</returns>
    public Task<MappingVersionRecord> GetAsync(long versionId, CancellationToken cancellationToken)
    {
        return versions.GetAsync(new { MappingVersionId = versionId }, cancellationToken);
    }

    /// <summary>
    /// Creates a draft with the set's next version number, pinned to the given dimension version: empty, or with the entries
    /// of <see cref="NewMappingVersionInput.CloneFromVersionId"/> remapped by member key.
    /// </summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="input">The pinned dimension version and the optional clone source.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft and the source keys a clone dropped.</returns>
    public async Task<CreatedMappingVersion> CreateAsync(long mappingSetId, NewMappingVersionInput input, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        var version = new MappingVersionRecord
        {
            MappingVersionId = ids.NextId(),
            MappingSetId = mappingSetId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            DimensionVersionId = input.DimensionVersionId,
            Status = PublicationStatuses.Draft
        };
        await versions.InsertAsync(version, cancellationToken);
        IReadOnlyList<string> dropped = [];
        if (input.CloneFromVersionId is { } sourceVersionId) dropped = await cloner.CloneAsync(sourceVersionId, version, cancellationToken);
        return new CreatedMappingVersion { Version = version, DroppedSourceKeys = dropped };
    }

    /// <summary>Deletes draft mapping versions: their entries first, then the versions.</summary>
    /// <param name="versionIds">The draft versions.</param>
    /// <param name="cancellationToken">Cancels the remaining deletes.</param>
    /// <returns>A task that completes when the versions are deleted.</returns>
    public async Task DeleteAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await entries.DeleteAsync(new { MappingVersionId = versionIds }, cancellationToken);
        await versions.DeleteAsync(new { MappingVersionId = versionIds }, cancellationToken);
    }
}
