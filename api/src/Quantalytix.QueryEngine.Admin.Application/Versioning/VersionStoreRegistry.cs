using System.Collections.Frozen;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>Finds the version store of each <c>version_kind</c>.</summary>
public sealed class VersionStoreRegistry(IEnumerable<IVersionStore> stores)
{
    /// <summary>The registered stores by kind.</summary>
    private readonly FrozenDictionary<string, IVersionStore> _stores = stores.ToFrozenDictionary(store => store.Kind, StringComparer.Ordinal);

    /// <summary>Returns the store of a kind.</summary>
    /// <param name="kind">A <c>version_kind</c> value.</param>
    /// <returns>The kind's store.</returns>
    public IVersionStore Get(string kind)
    {
        return _stores[kind];
    }
}
