using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The mapping set commands, which run on stored state, never inside a change set: create a mapping set with an empty live version
/// (OI-27) and delete a mapping set (B12). Both advance the semantic revision.
/// </summary>
public sealed class MappingCommands(
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionRecord> dimensions,
    MappingVersionRemover remover,
    MappingSetSnapshotLoader loader,
    LiveSnapshot live,
    SemanticRevision semanticRevision,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>Creates a mapping set onto an analytical categorical or hierarchical dimension (B1) with its empty live version.</summary>
    /// <param name="input">The new mapping set, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The mapping set and its version, or the refusal of a missing dimension (400), a taken key or another dimension (409).</returns>
    public async Task<Outcome<MappingSetCreated>> CreateAsync(CreateMappingSetInput input, CancellationToken cancellationToken)
    {
        if ((await dimensions.ListAsync(new { input.DimensionId }, cancellationToken)).SingleOrDefault() is not { } dimension)
            return Outcome<MappingSetCreated>.Refused(Refusal.Invalid(new Dictionary<string, string[]> { ["dimensionId"] = [$"Dimension {input.DimensionId} does not exist."] }));
        if (dimension.DimensionPurpose != DimensionPurposes.Analytical || dimension.DimensionKind is not (DimensionKinds.Categorical or DimensionKinds.Hierarchical))
            return Outcome<MappingSetCreated>.Refused(Refusal.Conflict(
                "Dimension cannot take mapping sets",
                $"Mapping sets target analytical categorical or hierarchical dimensions; '{dimension.DimensionKey}' is a {dimension.DimensionPurpose} {dimension.DimensionKind} dimension."));
        var keys = (await sets.ListAsync(null, cancellationToken)).Select(set => set.MappingSetKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (keys.Contains(input.Key))
            return Outcome<MappingSetCreated>.Refused(Refusal.Conflict("Key taken", $"Another mapping set already uses the key '{input.Key}'."));

        var set = new MappingSetRecord
        {
            MappingSetId = ids.NextId(),
            MappingSetKey = input.Key,
            SourceNamespace = input.SourceNamespace,
            DimensionId = dimension.DimensionId,
            DisplayName = input.DisplayName
        };
        await sets.InsertAsync(set, cancellationToken);
        var version = new MappingVersionRecord
        {
            MappingVersionId = ids.NextId(),
            MappingSetId = set.MappingSetId,
            SnapshotId = await live.IdAsync(cancellationToken),
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await versions.InsertAsync(version, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<MappingSetCreated>.Success(new MappingSetCreated { MappingSetId = set.MappingSetId, VersionId = version.MappingVersionId });
    }

    /// <summary>Deletes a mapping set nothing blocks (B12): its live version with its entries, then the row.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Null when deleted; otherwise why nothing was deleted.</returns>
    public async Task<Refusal?> DeleteAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadLiveAsync(mappingSetId, cancellationToken);
        var check = MappingDeleteRules.Set(snapshot);
        if (!check.Allowed) return Refusal.DeleteBlocked(check);

        await remover.RemoveVersionAsync(snapshot.VersionId, cancellationToken);
        await remover.RemoveIdentityAsync(mappingSetId, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }
}
