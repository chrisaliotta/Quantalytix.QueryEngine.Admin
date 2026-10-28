using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Resolves what an operation names in a dataset version. A stored id that names nothing (another author deleted it) refuses the
/// operation as a conflict (409), as a commit replayed onto newer rows does (OI-02).
/// </summary>
internal static class DatasetEditTargets
{
    /// <summary>The id an input names for the item it saves: its stored or temporary id, or a new one when it names none.</summary>
    /// <param name="id">The input's id.</param>
    /// <param name="context">Resolves the id.</param>
    /// <returns>The real id.</returns>
    public static long SavedId(string? id, ChangeContext context)
    {
        return id is null ? context.Ids.NewId() : context.Ids.Resolve(id);
    }

    /// <summary>The real id of an optional reference.</summary>
    /// <param name="id">A stored or temporary id, or null.</param>
    /// <param name="context">Resolves the id.</param>
    /// <returns>The real id, or null.</returns>
    public static long? Optional(string? id, ChangeContext context)
    {
        return id is null ? null : context.Ids.Resolve(id);
    }

    /// <summary>Whether an input names a stored item the version no longer has, refusing it when so.</summary>
    /// <param name="id">The input's id.</param>
    /// <param name="exists">Whether the version has the item.</param>
    /// <param name="what">The item's kind, such as <c>Field</c>.</param>
    /// <param name="context">Takes the refusal.</param>
    /// <returns>True when the stored item is gone.</returns>
    public static bool IsGone(string? id, bool exists, string what, ChangeContext context)
    {
        if (exists || id is null || TemporaryIds.IsTemporary(id)) return false;
        Missing(what, id, context);
        return true;
    }

    /// <summary>The item an operation names among a version's items.</summary>
    /// <typeparam name="T">The item's record.</typeparam>
    /// <param name="items">The version's items.</param>
    /// <param name="idOf">An item's id.</param>
    /// <param name="id">The named stored or temporary id.</param>
    /// <param name="what">The item's kind, such as <c>Grain</c>.</param>
    /// <param name="context">Resolves the id and takes the refusal.</param>
    /// <returns>The item, or null when the version has none with the id.</returns>
    public static T? Find<T>(IEnumerable<T> items, Func<T, long> idOf, string id, string what, ChangeContext context) where T : class
    {
        var resolved = context.Ids.Resolve(id);
        var found = items.SingleOrDefault(item => idOf(item) == resolved);
        if (found is null) Missing(what, id, context);
        return found;
    }

    /// <summary>Refuses an item key that differs from the stored item's, or that another item of the version has.</summary>
    /// <param name="stored">The stored item's key, or null for a new item.</param>
    /// <param name="key">The input's key.</param>
    /// <param name="taken">The keys of the version's other items.</param>
    /// <param name="what">The item's kind in lower case, such as <c>field</c>.</param>
    /// <param name="context">Takes the refusal.</param>
    /// <returns>True when the key is refused.</returns>
    public static bool IsKeyRefused(string? stored, string key, IEnumerable<string> taken, string what, ChangeContext context)
    {
        if (stored is not null && stored != key) context.Reject("input.key", $"The {what} key is fixed once created.");
        else if (stored is null && taken.Contains(key, StringComparer.Ordinal)) context.Reject("input.key", $"Another {what} of this version uses '{key}'.");
        return context.Refused;
    }

    /// <summary>Refuses an operation whose target is gone.</summary>
    private static void Missing(string what, string id, ChangeContext context)
    {
        context.Conflict($"{what} not found", $"{what} {id} is not in this version; it may have been deleted.");
    }
}
