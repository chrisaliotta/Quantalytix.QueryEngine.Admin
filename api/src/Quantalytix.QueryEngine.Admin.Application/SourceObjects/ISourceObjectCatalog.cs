namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>
/// Reads the tables, views and columns of the tenant database from its information_schema. This is the database
/// introspection seam: services, validators and rules read physical objects through it, so they can be tested without a
/// database.
/// </summary>
public interface ISourceObjectCatalog
{
    /// <summary>Lists every table and view of the tenant database, the query engine's metadata objects included.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tables and views, in no particular order.</returns>
    Task<IReadOnlyList<SourceObject>> ListObjectsAsync(CancellationToken cancellationToken);

    /// <summary>Lists the columns of one table or view of the tenant database.</summary>
    /// <param name="objectName">The table or view name; a name the database does not have has no columns.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The columns, in ordinal order.</returns>
    Task<IReadOnlyList<PhysicalColumn>> ListColumnsAsync(string objectName, CancellationToken cancellationToken);
}
