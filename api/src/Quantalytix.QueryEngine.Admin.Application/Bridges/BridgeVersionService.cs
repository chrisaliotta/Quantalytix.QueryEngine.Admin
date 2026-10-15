using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Reads, creates, clones, edits and deletes the versions of bridges. Status changes go through the version workflow.</summary>
public sealed class BridgeVersionService(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    ITableGateway<DimensionBindingRecord> bindings,
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
    /// Creates a draft with the bridge's next version number: empty, or with the mappings of
    /// <see cref="NewBridgeVersionInput.CloneFromVersionId"/> remapped by member key into the new endpoint versions.
    /// </summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="input">The endpoint versions, the allocation semantics and the optional clone source.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft and the mappings a clone dropped.</returns>
    public async Task<CreatedBridgeVersion> CreateAsync(long bridgeId, NewBridgeVersionInput input, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        var version = new BridgeVersionRecord
        {
            BridgeVersionId = ids.NextId(),
            BridgeId = bridgeId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            SourceDimensionVersionId = input.SourceDimensionVersionId,
            TargetDimensionVersionId = input.TargetDimensionVersionId,
            Cardinality = input.Cardinality,
            AllocationMode = input.AllocationMode,
            Status = PublicationStatuses.Draft
        };
        await versions.InsertAsync(version, cancellationToken);
        IReadOnlyList<DroppedBridgeMapping> dropped = [];
        if (input.CloneFromVersionId is { } sourceVersionId) dropped = await cloner.CloneAsync(sourceVersionId, version, cancellationToken);
        return new CreatedBridgeVersion { Version = version, DroppedMappings = dropped };
    }

    /// <summary>Replaces a draft version's endpoint versions, cardinality and allocation mode; its mappings stay.</summary>
    /// <param name="versionId">The draft bridge version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored version.</returns>
    public async Task<BridgeVersionRecord> UpdateAsync(long versionId, BridgeVersionInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(versionId, cancellationToken);
        var updated = current with
        {
            SourceDimensionVersionId = input.SourceDimensionVersionId,
            TargetDimensionVersionId = input.TargetDimensionVersionId,
            Cardinality = input.Cardinality,
            AllocationMode = input.AllocationMode
        };
        await versions.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a draft version that no binding uses: it clears the bridge's current-version pointer when that names the
    /// version, then deletes the mappings and the version.
    /// </summary>
    /// <param name="versionId">The draft bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Null when the version was deleted; otherwise the detail naming the bindings, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await GetAsync(versionId, cancellationToken);
        var blockers = new DeleteBlockers($"Bridge version v{version.VersionNumber}");
        blockers.AddNamed("used by bindings", await BindingNamesAsync([versionId], cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        var bridge = await bridges.GetAsync(new { version.BridgeId }, cancellationToken);
        if (bridge.CurrentBridgeVersionId == versionId) await bridges.UpdateAsync(bridge with { CurrentBridgeVersionId = null }, cancellationToken);
        await RemoveAsync([versionId], cancellationToken);
        return null;
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

    /// <summary>The display names of the dataset bindings that remap through any of the versions, quoted.</summary>
    /// <param name="versionIds">Bridge versions.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One name per binding.</returns>
    public async Task<IEnumerable<string>> BindingNamesAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        var boundBy = await bindings.ListAsync(new { BridgeVersionId = versionIds }, cancellationToken);
        return boundBy.Select(binding => $"'{binding.DisplayName}'");
    }
}
