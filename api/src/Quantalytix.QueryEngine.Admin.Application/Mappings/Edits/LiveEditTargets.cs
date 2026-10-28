using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Resolves what a mapping set or bridge operation names in its aggregate. A name that resolves to nothing (another author deleted
/// it, or the version is frozen) refuses the operation as a conflict (409), as a commit replayed onto newer rows does (OI-02).
/// </summary>
internal static class LiveEditTargets
{
    /// <summary>Whether an operation names the live version, the only one a change set edits.</summary>
    /// <param name="liveVersionId">The aggregate's live version.</param>
    /// <param name="versionId">The version the operation names.</param>
    /// <param name="owner">The mapping set or bridge, as messages name it.</param>
    /// <param name="context">Resolves the id and takes the refusal.</param>
    /// <returns>True when the operation may apply.</returns>
    public static bool IsLive(long liveVersionId, string versionId, string owner, ChangeContext context)
    {
        if (context.Ids.Resolve(versionId) == liveVersionId) return true;
        context.Conflict("Version is not live", $"Version {versionId} is not the live version of {owner}; frozen versions accept no edits.");
        return false;
    }

    /// <summary>The item an operation names by id.</summary>
    /// <typeparam name="T">The item's row.</typeparam>
    /// <param name="items">The aggregate's items.</param>
    /// <param name="idOf">An item's id.</param>
    /// <param name="id">The stored or temporary id the operation names.</param>
    /// <param name="what">The item's kind, such as <c>Entry</c>.</param>
    /// <param name="context">Resolves the id and takes the refusal.</param>
    /// <returns>The item, or null when the version has none with the id.</returns>
    public static T? Find<T>(IEnumerable<T> items, Func<T, long> idOf, string id, string what, ChangeContext context) where T : class
    {
        var resolved = context.Ids.Resolve(id);
        var found = items.SingleOrDefault(item => idOf(item) == resolved);
        if (found is null) context.Conflict($"{what} not found", $"{what} {id} is not in the live version; it may have been deleted.");
        return found;
    }

    /// <summary>
    /// Whether every input names an item of the version or a new one: a stored id the version no longer has refuses the operation.
    /// </summary>
    /// <param name="ids">The ids the inputs name.</param>
    /// <param name="stored">The ids of the version's items.</param>
    /// <param name="what">The item's kind, such as <c>Entry</c>.</param>
    /// <param name="context">Resolves the ids and takes the refusal.</param>
    /// <returns>True when every stored id is in the version.</returns>
    public static bool AllPresent(IEnumerable<string> ids, IReadOnlySet<long> stored, string what, ChangeContext context)
    {
        if (ids.Where(id => !TemporaryIds.IsTemporary(id)).FirstOrDefault(id => !stored.Contains(context.Ids.Resolve(id))) is not { } gone) return true;
        context.Conflict($"{what} not found", $"{what} {gone} is not in the live version; it may have been deleted.");
        return false;
    }
}
