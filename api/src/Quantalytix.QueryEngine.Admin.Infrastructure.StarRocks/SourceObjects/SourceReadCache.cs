using System.Collections.Concurrent;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SourceObjects;

/// <summary>
/// Keeps reads of tenant data for a short time: source object statistics (OI-34), which <c>information_schema.tables</c> makes
/// costly and StarRocks refreshes lazily anyway, and a field's distinct values, which a report's diagnostics read on every preview.
/// </summary>
/// <typeparam name="TValue">What is read.</typeparam>
/// <param name="time">The clock the entries expire by.</param>
public sealed class SourceReadCache<TValue>(TimeProvider time)
{
    /// <summary>How long a read is reused.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    /// <summary>Each read, with when it was made, by key.</summary>
    private readonly ConcurrentDictionary<string, (DateTime ReadAt, TValue Value)> _entries = new(StringComparer.Ordinal);

    /// <summary>A read, made again once the cached one is older than the lifetime.</summary>
    /// <param name="key">What is read, including the tenant.</param>
    /// <param name="read">Reads the value, stamped with the given time.</param>
    /// <returns>The value.</returns>
    public async Task<TValue> GetAsync(string key, Func<DateTime, Task<TValue>> read)
    {
        var now = time.GetUtcNow().UtcDateTime;
        if (_entries.TryGetValue(key, out var entry) && now - entry.ReadAt < Lifetime) return entry.Value;
        var value = await read(now);
        _entries[key] = (now, value);
        return value;
    }
}
