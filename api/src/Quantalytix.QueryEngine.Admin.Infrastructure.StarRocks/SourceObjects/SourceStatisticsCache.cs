using System.Collections.Concurrent;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SourceObjects;

/// <summary>
/// Keeps each tenant's source object statistics for a short time (OI-34): <c>information_schema.tables</c> is costly to read on every
/// list, and StarRocks refreshes its row counts lazily anyway.
/// </summary>
/// <param name="time">The clock the entries expire by.</param>
public sealed class SourceStatisticsCache(TimeProvider time)
{
    /// <summary>How long a tenant's statistics are reused.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    /// <summary>The statistics of each tenant, with when they were read.</summary>
    private readonly ConcurrentDictionary<string, (DateTime ReadAt, IReadOnlyDictionary<string, DatasetSourceObjectStats> Statistics)> _entries = new(StringComparer.Ordinal);

    /// <summary>A tenant's statistics, read again once the cached ones are older than the lifetime.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="read">Reads the statistics, stamped with the given time.</param>
    /// <returns>The statistics, by object name.</returns>
    public async Task<IReadOnlyDictionary<string, DatasetSourceObjectStats>> GetAsync(
        string tenantId,
        Func<DateTime, Task<IReadOnlyDictionary<string, DatasetSourceObjectStats>>> read)
    {
        var now = time.GetUtcNow().UtcDateTime;
        if (_entries.TryGetValue(tenantId, out var entry) && now - entry.ReadAt < Lifetime) return entry.Statistics;
        var statistics = await read(now);
        _entries[tenantId] = (now, statistics);
        return statistics;
    }
}
