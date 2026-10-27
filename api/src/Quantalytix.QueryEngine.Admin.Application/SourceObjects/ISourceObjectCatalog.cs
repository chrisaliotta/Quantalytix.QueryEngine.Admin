namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>
/// Reads the tables, views and columns of the tenant database from its <c>information_schema</c>: the database introspection seam,
/// so the dataset module can be tested without a database.
/// </summary>
public interface ISourceObjectCatalog
{
    /// <summary>Lists every table and view of the tenant database, the query engine's metadata objects included.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tables and views, in no particular order.</returns>
    Task<IReadOnlyList<SourceObject>> ListObjectsAsync(CancellationToken cancellationToken);

    /// <summary>Lists the columns of one table or view.</summary>
    /// <param name="objectName">The table or view; a name the database does not have has no columns.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The columns, in ordinal order.</returns>
    Task<IReadOnlyList<PhysicalColumn>> ListColumnsAsync(string objectName, CancellationToken cancellationToken);

    /// <summary>Lists the columns of every table and view, as one read for a change set's operations.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each object's columns in ordinal order, by object name.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyList<PhysicalColumn>>> ListAllColumnsAsync(CancellationToken cancellationToken);

    /// <summary>Reads the storage statistics StarRocks keeps for the tables and views, cached briefly (OI-34).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The statistics of each measured object, by object name.</returns>
    Task<IReadOnlyDictionary<string, DatasetSourceObjectStats>> ListStatisticsAsync(CancellationToken cancellationToken);
}
