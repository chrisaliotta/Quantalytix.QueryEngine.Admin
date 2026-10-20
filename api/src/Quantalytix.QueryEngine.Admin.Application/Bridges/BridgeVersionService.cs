using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Reads, creates, clones, edits and deletes the versions of bridges. Status changes go through the version workflow.</summary>
public sealed class BridgeVersionService(
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    BridgeVersionCloner cloner,
    IIdGenerator ids)
{
    /// <summary>Lists a bridge's versions.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The versions by version number.</returns>
    public async Task<IReadOnlyList<BridgeVersionRecord>> ListAsync(long bridgeId, CancellationToken cancellationToken)
    {
        var rows = await versions.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        return [.. rows.OrderBy(row => row.VersionNumber)];
    }

    /// <summary>Reads one bridge version.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version.</returns>
    public Task<BridgeVersionRecord> GetAsync(long versionId, CancellationToken cancellationToken)
    {
        return versions.GetAsync(new { BridgeVersionId = versionId }, cancellationToken);
    }

    /// <summary>
    /// Creates a draft with the bridge's next version number: empty, or with copies of the mappings of
    /// <see cref="NewBridgeVersionInput.CloneFromVersionId"/>.
    /// </summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="input">The allocation semantics and the optional clone source.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft.</returns>
    public async Task<BridgeVersionRecord> CreateAsync(long bridgeId, NewBridgeVersionInput input, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        var version = new BridgeVersionRecord
        {
            BridgeVersionId = ids.NextId(),
            BridgeId = bridgeId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            Cardinality = input.Cardinality,
            AllocationMode = input.AllocationMode,
            Status = PublicationStatuses.Draft
        };
        await versions.InsertAsync(version, cancellationToken);
        if (input.CloneFromVersionId is { } sourceVersionId) await cloner.CloneAsync(sourceVersionId, version, cancellationToken);
        return version;
    }

    /// <summary>Replaces a version's cardinality and allocation mode; its mappings stay.</summary>
    /// <param name="versionId">The draft or published bridge version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored version.</returns>
    public async Task<BridgeVersionRecord> UpdateAsync(long versionId, BridgeVersionInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(versionId, cancellationToken);
        var updated = current with { Cardinality = input.Cardinality, AllocationMode = input.AllocationMode };
        await versions.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Deletes a draft version with its mappings; nothing references a draft.</summary>
    /// <param name="versionId">The draft bridge version.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the version is gone.</returns>
    public Task DeleteAsync(long versionId, CancellationToken cancellationToken)
    {
        return RemoveAsync([versionId], cancellationToken);
    }

    /// <summary>Hard-deletes draft versions: their mappings first, then the versions.</summary>
    /// <param name="versionIds">The draft versions.</param>
    /// <param name="cancellationToken">Cancels the remaining deletes.</param>
    /// <returns>A task that completes when the versions are deleted.</returns>
    public async Task RemoveAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await mappings.DeleteAsync(new { BridgeVersionId = versionIds }, cancellationToken);
        await versions.DeleteAsync(new { BridgeVersionId = versionIds }, cancellationToken);
    }
}
