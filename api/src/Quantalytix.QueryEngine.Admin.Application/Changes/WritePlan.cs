using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>
/// The row writes of one commit, ordered so a commit that stops midway leaves a harmless residue, because StarRocks rejects
/// <c>UPDATE</c> and <c>DELETE</c> inside transactions (blueprint 01, OI-03): schema statements (DDL) first, because they cannot be
/// rolled back and the rows they enable must not be stored without them; then deletes children-first, then updates, then inserts
/// parents-first, then the root row's writes last. Writes at the same depth keep the order they were added in.
/// </summary>
public sealed class WritePlan
{
    /// <summary>The depth of the aggregate root row.</summary>
    public const int Root = 0;

    /// <summary>The writes, in the order they were added.</summary>
    private readonly List<PlannedWrite> _writes = [];

    /// <summary>Adds the inserts of new rows; nothing when there are none.</summary>
    /// <typeparam name="TRecord">The table's record.</typeparam>
    /// <param name="gateway">The table's gateway.</param>
    /// <param name="depth">The rows' depth below the root.</param>
    /// <param name="records">The new rows.</param>
    public void Insert<TRecord>(ITableGateway<TRecord> gateway, int depth, IReadOnlyList<TRecord> records) where TRecord : class
    {
        if (records.Count == 0) return;
        _writes.Add(new PlannedWrite(WriteKind.Insert, depth, typeof(TRecord), cancellationToken => gateway.InsertManyAsync(records, cancellationToken)));
    }

    /// <summary>Adds the update of one stored row.</summary>
    /// <typeparam name="TRecord">The table's record.</typeparam>
    /// <param name="gateway">The table's gateway.</param>
    /// <param name="depth">The row's depth below the root.</param>
    /// <param name="record">The row as it is to be stored.</param>
    public void Update<TRecord>(ITableGateway<TRecord> gateway, int depth, TRecord record) where TRecord : class
    {
        _writes.Add(new PlannedWrite(WriteKind.Update, depth, typeof(TRecord), cancellationToken => gateway.UpdateAsync(record, cancellationToken)));
    }

    /// <summary>Adds the delete of the rows a filter matches.</summary>
    /// <typeparam name="TRecord">The table's record.</typeparam>
    /// <param name="gateway">The table's gateway.</param>
    /// <param name="depth">The rows' depth below the root.</param>
    /// <param name="filter">The gateway filter, such as <c>new { DimensionMemberId = id }</c>.</param>
    public void Delete<TRecord>(ITableGateway<TRecord> gateway, int depth, object filter) where TRecord : class
    {
        _writes.Add(new PlannedWrite(WriteKind.Delete, depth, typeof(TRecord), cancellationToken => gateway.DeleteAsync(filter, cancellationToken)));
    }

    /// <summary>Adds a schema statement, such as a managed table's new columns, which runs before every row write.</summary>
    /// <param name="statement">Runs the statement.</param>
    public void Schema(Func<CancellationToken, Task> statement)
    {
        _writes.Add(new PlannedWrite(WriteKind.Schema, Root, null, statement));
    }

    /// <summary>
    /// Adds a write that is not one table gateway call, such as another module's public service writing rows the aggregate owns
    /// (a report source's bridge). It runs where a row write of its kind and depth would.
    /// </summary>
    /// <param name="kind">What the write does to its rows.</param>
    /// <param name="depth">The rows' depth below the root.</param>
    /// <param name="write">Makes the write.</param>
    public void Call(WriteKind kind, int depth, Func<CancellationToken, Task> write)
    {
        _writes.Add(new PlannedWrite(kind, depth, null, write));
    }

    /// <summary>
    /// Adds the writes that turn one table's stored rows into the changed rows: a delete per row that is gone, an update per row
    /// that differs, and one insert of the rows that are new. Rows are matched by their key.
    /// </summary>
    /// <typeparam name="TRecord">The table's record.</typeparam>
    /// <typeparam name="TKey">The key, an anonymous object of the primary key properties, which is also the delete filter.</typeparam>
    /// <param name="gateway">The table's gateway.</param>
    /// <param name="depth">The rows' depth below the root.</param>
    /// <param name="before">The stored rows of the aggregate.</param>
    /// <param name="after">The aggregate's rows after the operations.</param>
    /// <param name="keyOf">The key of a row, such as <c>row =&gt; new { row.DimensionMemberId }</c>.</param>
    public void Sync<TRecord, TKey>(
        ITableGateway<TRecord> gateway,
        int depth,
        IEnumerable<TRecord> before,
        IEnumerable<TRecord> after,
        Func<TRecord, TKey> keyOf)
        where TRecord : class
        where TKey : notnull
    {
        var stored = before.ToDictionary(keyOf);
        var changed = after.ToDictionary(keyOf);
        foreach (var key in stored.Keys.Where(key => !changed.ContainsKey(key))) Delete(gateway, depth, key);
        foreach (var (key, row) in changed)
        {
            if (stored.TryGetValue(key, out var original) && !EqualityComparer<TRecord>.Default.Equals(original, row)) Update(gateway, depth, row);
        }

        Insert(gateway, depth, [.. changed.Where(entry => !stored.ContainsKey(entry.Key)).Select(entry => entry.Value)]);
    }

    /// <summary>The writes in commit order.</summary>
    /// <returns>
    /// Schema statements, then child deletes deepest first, child updates and inserts shallowest first, then the root's deletes, updates
    /// and inserts.
    /// </returns>
    public IReadOnlyList<PlannedWrite> Ordered()
    {
        var rows = _writes.Where(write => write.Kind != WriteKind.Schema).ToList();
        var children = rows.Where(write => write.Depth != Root).ToList();
        return
        [
            .. _writes.Where(write => write.Kind == WriteKind.Schema),
            .. children.Where(write => write.Kind == WriteKind.Delete).OrderByDescending(write => write.Depth),
            .. children.Where(write => write.Kind == WriteKind.Update).OrderBy(write => write.Depth),
            .. children.Where(write => write.Kind == WriteKind.Insert).OrderBy(write => write.Depth),
            .. rows.Where(write => write.Depth == Root).OrderBy(write => write.Kind)
        ];
    }
}
