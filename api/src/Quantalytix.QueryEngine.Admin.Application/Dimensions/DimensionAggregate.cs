using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The dimension change-set aggregate (blueprint 12): the identity row, its roles and the live version's levels, members and
/// bands, saved together. A locked profile refuses every operation (M11); a version operation must name the live version. The
/// closure is rebuilt whenever members change. Every saved change is engine-visible except a change of removability alone.
/// </summary>
public sealed class DimensionAggregate(
    DimensionSnapshotLoader loader,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    ITableGateway<DimensionClosureRecord> closure)
    : IChangeSetAggregate<DimensionSnapshot, DimensionOperation, DimensionEditState>
{
    /// <summary>The depth of roles and the version below the dimension row.</summary>
    private const int VersionDepth = 1;

    /// <summary>The depth of levels and members.</summary>
    private const int ContentDepth = 2;

    /// <summary>The depth of bands and closure rows, which name members.</summary>
    private const int MemberDependentDepth = 3;

    /// <summary>Reads the dimension with its live version.</summary>
    /// <param name="aggregateId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public Task<DimensionSnapshot> LoadAsync(long aggregateId, CancellationToken cancellationToken)
    {
        return loader.LoadLiveAsync(aggregateId, cancellationToken);
    }

    /// <summary>Applies one operation.</summary>
    /// <param name="snapshot">The aggregate so far.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the operation applied.</returns>
    public DimensionSnapshot Apply(DimensionSnapshot snapshot, DimensionOperation operation, ChangeContext context)
    {
        if (snapshot.EditPolicy == DimensionEditPolicies.Locked)
        {
            context.Conflict(DimensionRefusals.ReadOnlyTitle, DimensionRefusals.ReadOnlyDetail(snapshot.Dimension, snapshot.Profile!));
            return snapshot;
        }

        if (operation is DimensionVersionOperation targeted && !DimensionEditTargets.IsLive(snapshot, targeted, context)) return snapshot;
        return operation switch
        {
            DimensionUpdateDimensionOperation update => DimensionIdentityEdits.Update(snapshot, update, context),
            DimensionUpdateVersionOperation update => DimensionIdentityEdits.UpdateVersion(snapshot, update, context),
            DimensionSaveMemberOperation save => DimensionMemberEdits.Save(snapshot, save, context),
            DimensionDeleteMemberOperation delete => DimensionMemberEdits.Delete(snapshot, delete, context),
            DimensionMoveMemberOperation move => DimensionMemberEdits.Move(snapshot, move, context),
            DimensionImportMembersOperation import => DimensionMemberEdits.Import(snapshot, import, context),
            DimensionClearMembersOperation => DimensionMemberEdits.Clear(snapshot, context),
            DimensionSaveLevelOperation save => DimensionLevelEdits.Save(snapshot, save, context),
            DimensionDeleteLevelOperation delete => DimensionLevelEdits.Delete(snapshot, delete, context),
            DimensionReorderLevelsOperation reorder => DimensionLevelEdits.Reorder(snapshot, reorder, context),
            DimensionSaveBandsOperation save => DimensionBandEdits.Save(snapshot, save, context),
            DimensionSaveRoleOperation save => DimensionIdentityEdits.SaveRole(snapshot, save, context),
            DimensionDeleteRoleOperation delete => DimensionIdentityEdits.DeleteRole(snapshot, delete, context),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Not a dimension operation.")
        };
    }

    /// <summary>Plans the row writes, the dimension row last.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <param name="plan">The plan.</param>
    public void Plan(DimensionSnapshot before, DimensionSnapshot after, WritePlan plan)
    {
        if (before.Dimension != after.Dimension) plan.Update(dimensions, WritePlan.Root, after.Dimension);
        plan.Sync(roles, VersionDepth, before.Roles, after.Roles, row => new { row.DimensionRoleId });
        if (before.Version != after.Version) plan.Update(versions, VersionDepth, after.Version);
        plan.Sync(levels, ContentDepth, before.Levels, after.Levels, row => new { row.DimensionLevelId });
        plan.Sync(members, ContentDepth, before.Members, after.Members, row => new { row.DimensionMemberId });
        plan.Sync(bands, MemberDependentDepth, before.Bands, after.Bands, row => new { row.DimensionVersionId, row.Ordinal });
        if (Same(before.Members, after.Members)) return;
        plan.Delete(closure, MemberDependentDepth, new { after.Version.DimensionVersionId });
        plan.Insert(closure, MemberDependentDepth, ClosureBuilder.ClosureOf(after.VersionId, after.Members));
    }

    /// <summary>Whether the engine reads the change: anything but the removability flag.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <returns>True when the commit advances the semantic revision.</returns>
    public bool IsEngineVisible(DimensionSnapshot before, DimensionSnapshot after)
    {
        return before.Dimension with { IsRemovable = after.Dimension.IsRemovable } != after.Dimension
            || before.Version != after.Version
            || !Same(before.Roles, after.Roles)
            || !Same(before.Levels, after.Levels)
            || !Same(before.Members, after.Members)
            || !Same(before.Bands, after.Bands);
    }

    /// <summary>The edit state of a snapshot.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <param name="cancellationToken">Unused; the snapshot holds everything.</param>
    /// <returns>The edit state.</returns>
    public Task<DimensionEditState> ReadAsync(DimensionSnapshot snapshot, Func<long, string> presentId, CancellationToken cancellationToken)
    {
        return Task.FromResult(DimensionReadModels.EditState(snapshot, presentId));
    }

    /// <summary>Whether two row lists hold the same rows, in any order.</summary>
    private static bool Same<TRecord>(IReadOnlyList<TRecord> before, IReadOnlyList<TRecord> after)
    {
        return before.Count == after.Count && before.ToHashSet().SetEquals(after);
    }
}
