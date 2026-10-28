using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The analytical bridge change-set aggregate (blueprint 13): the identity row and the live version's settings and mappings, saved
/// together. A version operation must name the live version. Report bridges never get here (<see cref="BridgeChanges"/>). Every saved
/// change is engine-visible, because the engine caches bridges with their availability and reads the live settings and mappings.
/// </summary>
public sealed class BridgeAggregate(
    BridgeSnapshotLoader loader,
    SnapshotRuleRunner<BridgeSnapshot> rules,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings)
    : IChangeSetAggregate<BridgeSnapshot, BridgeOperation, BridgeEditState>
{
    /// <summary>The depth of the version below the bridge row.</summary>
    private const int VersionDepth = 1;

    /// <summary>The depth of the mappings.</summary>
    private const int MappingDepth = 2;

    /// <summary>Reads the bridge with its live version.</summary>
    /// <param name="aggregateId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public Task<BridgeSnapshot> LoadAsync(long aggregateId, CancellationToken cancellationToken)
    {
        return loader.LoadLiveAsync(aggregateId, cancellationToken);
    }

    /// <summary>Applies one operation.</summary>
    /// <param name="snapshot">The aggregate so far.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the operation applied.</returns>
    public BridgeSnapshot Apply(BridgeSnapshot snapshot, BridgeOperation operation, ChangeContext context)
    {
        if (operation is BridgeVersionOperation targeted
            && !LiveEditTargets.IsLive(snapshot.VersionId, targeted.VersionId, $"bridge '{snapshot.Bridge.BridgeKey}'", context))
            return snapshot;
        return operation switch
        {
            BridgeUpdateOperation update => BridgeEdits.Update(snapshot, update),
            BridgeVersionUpdateOperation update => BridgeEdits.UpdateVersion(snapshot, update),
            BridgeMappingSaveOperation save => BridgeEdits.Save(snapshot, save, context),
            BridgeMappingDeleteOperation delete => BridgeEdits.Delete(snapshot, delete, context),
            BridgeMappingsReplaceOperation replace => BridgeEdits.Replace(snapshot, replace, context),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Not a bridge operation.")
        };
    }

    /// <summary>Plans the row writes, the bridge row last.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <param name="plan">The plan.</param>
    public void Plan(BridgeSnapshot before, BridgeSnapshot after, WritePlan plan)
    {
        if (before.Bridge != after.Bridge) plan.Update(bridges, WritePlan.Root, after.Bridge);
        if (before.Version != after.Version) plan.Update(versions, VersionDepth, after.Version);
        plan.Sync(mappings, MappingDepth, before.Mappings, after.Mappings, row => new { row.BridgeMappingId });
    }

    /// <summary>Whether the engine reads the change: any change of the identity, the settings or the mappings.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <returns>True when the commit advances the semantic revision.</returns>
    public bool IsEngineVisible(BridgeSnapshot before, BridgeSnapshot after)
    {
        return before.Bridge != after.Bridge
            || before.Version != after.Version
            || before.Mappings.Count != after.Mappings.Count
            || !before.Mappings.ToHashSet().SetEquals(after.Mappings);
    }

    /// <summary>The edit state of a snapshot, with the findings of its settings and mappings.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="presentId">Writes each mapping id.</param>
    /// <param name="cancellationToken">Cancels the rules.</param>
    /// <returns>The edit state.</returns>
    public async Task<BridgeEditState> ReadAsync(BridgeSnapshot snapshot, Func<long, string> presentId, CancellationToken cancellationToken)
    {
        return BridgeReadModels.EditState(snapshot, await rules.EvaluateAsync(snapshot, cancellationToken), presentId);
    }
}
