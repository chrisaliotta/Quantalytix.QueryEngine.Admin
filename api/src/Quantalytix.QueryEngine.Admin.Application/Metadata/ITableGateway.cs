namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// Reads and writes the rows of the table one metadata record mirrors. Each call is a single statement: StarRocks rejects
/// UPDATE and DELETE inside transactions, so multi-table writes run as ordered single statements.
/// </summary>
/// <remarks>
/// A filter is an anonymous object whose property names are record property names. Each property becomes
/// <c>column = @Property</c>; a collection value (other than a string) becomes <c>column IN @Property</c>. An empty collection
/// matches nothing, so the call returns or changes nothing without reaching the database.
/// </remarks>
/// <typeparam name="TRecord">A record from the Metadata feature that mirrors one table.</typeparam>
public interface ITableGateway<TRecord> where TRecord : class
{
    /// <summary>Lists the rows that match a filter.</summary>
    /// <param name="filter">Null for every row; otherwise an anonymous object of record property values to match.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The matching rows, in no particular order.</returns>
    Task<IReadOnlyList<TRecord>> ListAsync(object? filter, CancellationToken cancellationToken);

    /// <summary>Reads the one row a primary key identifies.</summary>
    /// <param name="key">An anonymous object holding every key property, such as <c>new { DatasetId = id }</c>.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The row.</returns>
    Task<TRecord> GetAsync(object key, CancellationToken cancellationToken);

    /// <summary>Inserts one row. StarRocks treats an insert into a primary key table as an upsert.</summary>
    /// <param name="record">The row to write.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the row is written.</returns>
    Task InsertAsync(TRecord record, CancellationToken cancellationToken);

    /// <summary>Inserts rows with one multi-row INSERT per 500 rows; does nothing when there are none.</summary>
    /// <param name="records">The rows to write.</param>
    /// <param name="cancellationToken">Cancels the remaining statements.</param>
    /// <returns>A task that completes when every row is written.</returns>
    Task InsertManyAsync(IReadOnlyCollection<TRecord> records, CancellationToken cancellationToken);

    /// <summary>
    /// Sets every non-key column of the row the record's key identifies. A record whose columns are all key columns has
    /// nothing to update; delete and insert it instead.
    /// </summary>
    /// <param name="record">The row's new values, including its key.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the row is updated.</returns>
    Task UpdateAsync(TRecord record, CancellationToken cancellationToken);

    /// <summary>Deletes the rows that match a filter.</summary>
    /// <param name="filter">An anonymous object of record property values to match; required, so a call never empties a table.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    Task DeleteAsync(object filter, CancellationToken cancellationToken);
}
