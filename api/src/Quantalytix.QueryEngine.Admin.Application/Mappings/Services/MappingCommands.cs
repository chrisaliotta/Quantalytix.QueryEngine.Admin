using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The mapping set lifecycle commands, which run on stored state, never inside a change set: create a mapping set with an empty live
/// version 1 (OI-27), delete a mapping set or a frozen version (B12), and freeze the live version (OI-14). Version 1 is published
/// through the version workflow, so it is live and pinned by the active assumption set, as every later live version is.
/// </summary>
public sealed class MappingCommands(
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<AssumptionSetMappingRecord> pins,
    MappingDrafts drafts,
    MappingVersionRemover remover,
    MappingSetSnapshotLoader loader,
    VersionWorkflow workflow,
    LivingVersionFreezer freezer,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>Creates a mapping set onto an analytical categorical or hierarchical dimension (B1) with its empty live version 1.</summary>
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
        var draft = await drafts.CreateAsync(set.MappingSetId, null, cancellationToken);
        await workflow.PublishAsync(VersionKinds.MappingVersion, draft.MappingVersionId, cancellationToken);
        return Outcome<MappingSetCreated>.Success(new MappingSetCreated { MappingSetId = set.MappingSetId, VersionId = draft.MappingVersionId });
    }

    /// <summary>Deletes a mapping set nothing blocks (B12): its active-set pin, its versions with their entries, then the row.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Null when deleted; otherwise why nothing was deleted.</returns>
    public async Task<Refusal?> DeleteAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadLiveAsync(mappingSetId, cancellationToken);
        var check = MappingDeleteRules.Set(snapshot);
        if (!check.Allowed) return Refusal.DeleteBlocked(check);

        await pins.DeleteAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        await remover.RemoveAsync([.. snapshot.Surroundings.Versions.Select(version => version.MappingVersionId)], cancellationToken);
        await sets.DeleteAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }

    /// <summary>Deletes a frozen version that no historical assumption set pins, with its entries.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Null when deleted; otherwise why nothing was deleted.</returns>
    public async Task<Refusal?> DeleteVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        var check = MappingDeleteRules.Version(await loader.LoadVersionAsync(versionId, cancellationToken));
        if (!check.Allowed) return Refusal.DeleteBlocked(check);
        await remover.RemoveAsync([versionId], cancellationToken);
        return null;
    }

    /// <summary>Freezes the live version with a note and continues in a live clone (OI-14, OI-15).</summary>
    /// <param name="versionId">The live version.</param>
    /// <param name="note">Why it is frozen; blank is none.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The report and the new live version, or a 409 or 422 refusal with nothing changed.</returns>
    public async Task<Outcome<MappingFreezeResult>> FreezeAsync(long versionId, string? note, CancellationToken cancellationToken)
    {
        var frozen = await freezer.FreezeAsync(VersionKinds.MappingVersion, versionId, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), cancellationToken);
        if (!frozen.Succeeded) return Outcome<MappingFreezeResult>.Refused(frozen.Refusal);
        var live = await versions.GetAsync(new { MappingVersionId = frozen.Value.LiveVersionId }, cancellationToken);
        var entryCount = (await entries.ListAsync(new { live.MappingVersionId }, cancellationToken)).Count;
        return Outcome<MappingFreezeResult>.Success(new MappingFreezeResult { Report = frozen.Value.Report, Live = MappingReadModels.Summary(live, entryCount) });
    }
}
