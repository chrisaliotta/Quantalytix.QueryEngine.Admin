using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The mapping set change-set aggregate (blueprint 13): the identity row and the live version's entries, saved together. An entry
/// operation must name the live version. Every saved change is engine-visible, because the engine caches mapping sets with their
/// namespace and reads the live entries.
/// </summary>
public sealed class MappingSetAggregate(
    MappingSetSnapshotLoader loader,
    SnapshotRuleRunner<MappingSetSnapshot> rules,
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<DimensionMappingRecord> entries)
    : IChangeSetAggregate<MappingSetSnapshot, MappingOperation, MappingSetEditState>
{
    /// <summary>The depth of the entries below the mapping set row.</summary>
    private const int EntryDepth = 1;

    /// <summary>Reads the mapping set with its live version.</summary>
    /// <param name="aggregateId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public Task<MappingSetSnapshot> LoadAsync(long aggregateId, CancellationToken cancellationToken)
    {
        return loader.LoadLiveAsync(aggregateId, cancellationToken);
    }

    /// <summary>Applies one operation.</summary>
    /// <param name="snapshot">The aggregate so far.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the operation applied.</returns>
    public MappingSetSnapshot Apply(MappingSetSnapshot snapshot, MappingOperation operation, ChangeContext context)
    {
        if (operation is MappingVersionOperation targeted
            && !LiveEditTargets.IsLive(snapshot.VersionId, targeted.VersionId, $"mapping set '{snapshot.Set.MappingSetKey}'", context))
            return snapshot;
        return operation switch
        {
            MappingSetUpdateOperation update => MappingEdits.Update(snapshot, update),
            MappingEntrySaveOperation save => MappingEdits.Save(snapshot, save, context),
            MappingEntryDeleteOperation delete => MappingEdits.Delete(snapshot, delete, context),
            MappingEntriesReplaceOperation replace => MappingEdits.Replace(snapshot, replace, context),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Not a mapping set operation.")
        };
    }

    /// <summary>Plans the row writes, the mapping set row last.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <param name="plan">The plan.</param>
    public void Plan(MappingSetSnapshot before, MappingSetSnapshot after, WritePlan plan)
    {
        if (before.Set != after.Set) plan.Update(sets, WritePlan.Root, after.Set);
        plan.Sync(entries, EntryDepth, before.Entries, after.Entries, row => new { row.DimensionMappingId });
    }

    /// <summary>Whether the engine reads the change: any change of the identity or the entries.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <returns>True when the commit advances the semantic revision.</returns>
    public bool IsEngineVisible(MappingSetSnapshot before, MappingSetSnapshot after)
    {
        return before.Set != after.Set || before.Entries.Count != after.Entries.Count || !before.Entries.ToHashSet().SetEquals(after.Entries);
    }

    /// <summary>The edit state of a snapshot, with the findings of its entries.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="presentId">Writes each entry id.</param>
    /// <param name="cancellationToken">Cancels the rules.</param>
    /// <returns>The edit state.</returns>
    public async Task<MappingSetEditState> ReadAsync(MappingSetSnapshot snapshot, Func<long, string> presentId, CancellationToken cancellationToken)
    {
        return MappingReadModels.EditState(snapshot, await rules.EvaluateAsync(snapshot, cancellationToken), presentId);
    }
}
