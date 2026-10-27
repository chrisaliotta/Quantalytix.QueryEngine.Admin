using System.Collections;
using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

namespace Quantalytix.QueryEngine.Admin.Tests.Support;

/// <summary>
/// A database-free <see cref="ITableGateway{TRecord}"/> over a list, for service tests. It keeps the StarRocks gateway's
/// semantics: rows are identified by the record's primary key in <see cref="TableCatalog"/>; a filter property matches its
/// record property by SQL equality (null matches nothing, numbers compare by value), or with IN when it holds a collection
/// other than a string, so an empty collection matches nothing; inserts upsert by key like a StarRocks primary key table; an
/// update replaces the row with the record's key and changes nothing when there is none; a delete removes the matching rows.
/// </summary>
/// <example>
/// Seed through the constructor, pass the gateway where the service expects <see cref="ITableGateway{TRecord}"/>, then assert
/// on <see cref="All"/>:
/// <code>
/// var versions = new InMemoryTableGateway&lt;DimensionVersionRecord&gt;(draft, published);
/// var store = new DimensionVersionStore(versions);
/// await store.SetStatusAsync(draft.DimensionVersionId, PublicationStatuses.Validated, DateTime.UnixEpoch, cancellationToken);
/// Assert.Equal(PublicationStatuses.Validated, versions.All.Single(row => row.DimensionVersionId == draft.DimensionVersionId).Status);
/// </code>
/// </example>
/// <typeparam name="TRecord">A record from the Metadata feature.</typeparam>
public sealed class InMemoryTableGateway<TRecord> : ITableGateway<TRecord> where TRecord : class
{
    /// <summary>The record's properties by name, which filter properties name.</summary>
    private static readonly FrozenDictionary<string, PropertyInfo> Properties =
        DeclaredProperties.Of(typeof(TRecord)).ToFrozenDictionary(property => property.Name, StringComparer.Ordinal);

    /// <summary>The record's primary key properties, in key order.</summary>
    private static readonly IReadOnlyList<PropertyInfo> KeyProperties = [.. TableCatalog.For<TRecord>().KeyProperties.Select(name => Properties[name])];

    /// <summary>The table's rows.</summary>
    private readonly List<TRecord> _rows = [];

    /// <summary>Creates a table holding the given rows; rows that share a key keep the last one, as inserts would.</summary>
    /// <param name="rows">The initial rows, if any.</param>
    public InMemoryTableGateway(params IEnumerable<TRecord> rows)
    {
        foreach (var row in rows) Upsert(row);
    }

    /// <summary>A snapshot of every row, in insertion order, for assertions.</summary>
    public IReadOnlyList<TRecord> All
    {
        get
        {
            return [.. _rows];
        }
    }

    /// <summary>Lists the rows that match a filter.</summary>
    /// <param name="filter">Null for every row; otherwise an anonymous object of record property values to match.</param>
    /// <param name="cancellationToken">Unused; the list answers synchronously.</param>
    /// <returns>A snapshot of the matching rows.</returns>
    public Task<IReadOnlyList<TRecord>> ListAsync(object? filter, CancellationToken cancellationToken)
    {
        if (filter is null) return Task.FromResult(All);
        return Task.FromResult<IReadOnlyList<TRecord>>([.. _rows.Where(row => Matches(row, filter))]);
    }

    /// <summary>Reads the one row a primary key identifies; throws when no row or several rows match, as the database read does.</summary>
    /// <param name="key">An anonymous object holding every key property, such as <c>new { DatasetId = id }</c>.</param>
    /// <param name="cancellationToken">Unused; the list answers synchronously.</param>
    /// <returns>The row.</returns>
    public Task<TRecord> GetAsync(object key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_rows.Single(row => Matches(row, key)));
    }

    /// <summary>Inserts one row, replacing the row that has the same key.</summary>
    /// <param name="record">The row to write.</param>
    /// <param name="cancellationToken">Unused; the list changes synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task InsertAsync(TRecord record, CancellationToken cancellationToken)
    {
        Upsert(record);
        return Task.CompletedTask;
    }

    /// <summary>Inserts rows in order, each replacing the row that has the same key.</summary>
    /// <param name="records">The rows to write.</param>
    /// <param name="cancellationToken">Unused; the list changes synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task InsertManyAsync(IReadOnlyCollection<TRecord> records, CancellationToken cancellationToken)
    {
        foreach (var record in records) Upsert(record);
        return Task.CompletedTask;
    }

    /// <summary>Replaces the row that has the record's key; changes nothing when no row has it, as an UPDATE would.</summary>
    /// <param name="record">The row's new values, including its key.</param>
    /// <param name="cancellationToken">Unused; the list changes synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task UpdateAsync(TRecord record, CancellationToken cancellationToken)
    {
        var index = _rows.FindIndex(row => SameKey(row, record));
        if (index >= 0) _rows[index] = record;
        return Task.CompletedTask;
    }

    /// <summary>Deletes the rows that match a filter.</summary>
    /// <param name="filter">An anonymous object of record property values to match.</param>
    /// <param name="cancellationToken">Unused; the list changes synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task DeleteAsync(object filter, CancellationToken cancellationToken)
    {
        _rows.RemoveAll(row => Matches(row, filter));
        return Task.CompletedTask;
    }

    /// <summary>Replaces the row that has the record's key, or appends the record when no row has it.</summary>
    private void Upsert(TRecord record)
    {
        var index = _rows.FindIndex(row => SameKey(row, record));
        if (index >= 0) _rows[index] = record;
        else _rows.Add(record);
    }

    /// <summary>Whether two rows have the same primary key.</summary>
    private static bool SameKey(TRecord row, TRecord record)
    {
        return KeyProperties.All(key => Equals(key.GetValue(row), key.GetValue(record)));
    }

    /// <summary>Whether a row satisfies every condition of a filter.</summary>
    private static bool Matches(TRecord row, object filter)
    {
        return TableFilter.PropertiesOf(filter).All(condition => Matches(row, condition, condition.GetValue(filter)));
    }

    /// <summary>Whether a row satisfies one filter condition: IN for a collection other than a string, otherwise equality.</summary>
    private static bool Matches(TRecord row, PropertyInfo condition, object? value)
    {
        var stored = Properties[condition.Name].GetValue(row);
        if (TableFilter.IsCollection(condition)) return ((IEnumerable)value!).Cast<object?>().Any(candidate => SqlEquals(stored, candidate));
        return SqlEquals(stored, value);
    }

    /// <summary>SQL equality: NULL equals nothing, and a value converts to the stored value's type first, so <c>42</c> matches a BIGINT 42.</summary>
    private static bool SqlEquals(object? stored, object? candidate)
    {
        if (stored is null || candidate is null) return false;
        return Equals(stored, Convert.ChangeType(candidate, stored.GetType(), CultureInfo.InvariantCulture));
    }
}
