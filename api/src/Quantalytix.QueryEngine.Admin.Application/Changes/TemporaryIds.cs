using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>
/// Resolves the ids operations carry: a stored id is its decimal digits; an item created earlier in the same change set is
/// named by the client's temporary id, <c>tmp:&lt;uuid&gt;</c>, which gets one real id the first time it is resolved. The
/// commit answers the temporary-to-real map as <see cref="ChangeSetCommit{TState}.IdMap"/>. A preview presents the items it created by
/// their temporary ids (<see cref="Present"/>), because the real ids it allocates are not kept, and the client's next preview names
/// those items by the temporary ids again.
/// </summary>
/// <param name="ids">Allocates the real ids.</param>
public sealed class TemporaryIds(IIdGenerator ids)
{
    /// <summary>The prefix of every temporary id.</summary>
    public const string Prefix = "tmp:";

    /// <summary>The real id of every temporary id resolved so far.</summary>
    private readonly Dictionary<string, long> _map = new(StringComparer.Ordinal);

    /// <summary>The temporary id of every real id allocated for one.</summary>
    private readonly Dictionary<long, string> _presented = [];

    /// <summary>The real id of every temporary id resolved so far, by temporary id.</summary>
    public IReadOnlyDictionary<string, long> Map => _map;

    /// <summary>Whether an id is a temporary id.</summary>
    /// <param name="id">An id from an operation.</param>
    /// <returns>True for <c>tmp:&lt;uuid&gt;</c>.</returns>
    public static bool IsTemporary(string id)
    {
        return id.StartsWith(Prefix, StringComparison.Ordinal);
    }

    /// <summary>A stored id as the API writes it: its decimal digits.</summary>
    /// <param name="id">A stored id.</param>
    /// <returns>The id's digits.</returns>
    public static string PresentStored(long id)
    {
        return id.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>An id as a preview writes it: the temporary id it was allocated for, or its digits when it was stored.</summary>
    /// <param name="id">A real id, stored or allocated by this change set.</param>
    /// <returns>The temporary id or the digits.</returns>
    public string Present(long id)
    {
        return _presented.TryGetValue(id, out var temporary) ? temporary : PresentStored(id);
    }

    /// <summary>A real id for an item an operation creates without naming it, which no later operation can reference.</summary>
    /// <returns>The new id.</returns>
    public long NewId()
    {
        return ids.NextId();
    }

    /// <summary>The real id an operation's id names, allocating one for a temporary id seen for the first time.</summary>
    /// <param name="id">A stored id's digits or a temporary id.</param>
    /// <returns>The real id.</returns>
    public long Resolve(string id)
    {
        if (!IsTemporary(id)) return long.Parse(id, NumberStyles.None, CultureInfo.InvariantCulture);
        if (!_map.TryGetValue(id, out var real))
        {
            real = ids.NextId();
            _map[id] = real;
            _presented[real] = id;
        }

        return real;
    }
}
