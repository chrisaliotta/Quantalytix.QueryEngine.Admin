using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Resolves what an operation names in the aggregate. A name that resolves to nothing (another author deleted it, or the version
/// is frozen) refuses the operation as a conflict (409), as a commit replayed onto newer rows does (OI-02).
/// </summary>
internal static class DimensionEditTargets
{
    /// <summary>Whether an operation names the live version, the only one a change set edits.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Takes the refusal.</param>
    /// <returns>True when the operation may apply.</returns>
    public static bool IsLive(DimensionSnapshot snapshot, DimensionVersionOperation operation, ChangeContext context)
    {
        if (context.Ids.Resolve(operation.VersionId) == snapshot.VersionId) return true;
        context.Conflict(
            "Version is not live",
            $"Version {operation.VersionId} is not the live version of dimension '{snapshot.Dimension.DimensionKey}'; frozen versions accept no edits.");
        return false;
    }

    /// <summary>The member an operation names.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="id">The member's stored or temporary id.</param>
    /// <param name="context">Resolves the id and takes the refusal.</param>
    /// <returns>The member, or null when the version has none with the id.</returns>
    public static DimensionMemberRecord? Member(DimensionSnapshot snapshot, string id, ChangeContext context)
    {
        var memberId = context.Ids.Resolve(id);
        return snapshot.Members.SingleOrDefault(member => member.DimensionMemberId == memberId) ?? Missing<DimensionMemberRecord>("Member", id, context);
    }

    /// <summary>The level an operation names.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="id">The level's stored or temporary id.</param>
    /// <param name="context">Resolves the id and takes the refusal.</param>
    /// <returns>The level, or null when the version has none with the id.</returns>
    public static DimensionLevelRecord? Level(DimensionSnapshot snapshot, string id, ChangeContext context)
    {
        var levelId = context.Ids.Resolve(id);
        return snapshot.Levels.SingleOrDefault(level => level.DimensionLevelId == levelId) ?? Missing<DimensionLevelRecord>("Level", id, context);
    }

    /// <summary>The role an operation names.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="id">The role's stored or temporary id.</param>
    /// <param name="context">Resolves the id and takes the refusal.</param>
    /// <returns>The role, or null when the dimension has none with the id.</returns>
    public static DimensionRoleRecord? Role(DimensionSnapshot snapshot, string id, ChangeContext context)
    {
        var roleId = context.Ids.Resolve(id);
        return snapshot.Roles.SingleOrDefault(role => role.DimensionRoleId == roleId) ?? Missing<DimensionRoleRecord>("Role", id, context);
    }

    /// <summary>The id an input names for the item it saves: its stored or temporary id, or a new one when it names none.</summary>
    /// <param name="id">The input's id.</param>
    /// <param name="context">Resolves the id.</param>
    /// <returns>The real id.</returns>
    public static long SavedId(string? id, ChangeContext context)
    {
        return id is null ? context.Ids.NewId() : context.Ids.Resolve(id);
    }

    /// <summary>Whether an input names a stored item that the aggregate no longer has, refusing it when so.</summary>
    /// <param name="id">The input's id.</param>
    /// <param name="exists">Whether the aggregate has the item.</param>
    /// <param name="what">The item's kind, such as <c>Member</c>.</param>
    /// <param name="context">Takes the refusal.</param>
    /// <returns>True when the stored item is gone.</returns>
    public static bool IsGone(string? id, bool exists, string what, ChangeContext context)
    {
        if (exists || id is null || TemporaryIds.IsTemporary(id)) return false;
        Missing<object>(what, id, context);
        return true;
    }

    /// <summary>Refuses an operation whose target is gone.</summary>
    private static T? Missing<T>(string what, string id, ChangeContext context) where T : class
    {
        context.Conflict($"{what} not found", $"{what} {id} is not in the dimension; it may have been deleted.");
        return null;
    }
}
