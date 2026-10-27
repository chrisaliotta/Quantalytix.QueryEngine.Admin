using Quantalytix.QueryEngine.Admin.Application.Datasets;

namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>
/// Runs the DDL of the managed tables the API creates for new datasets (OI-33). Callers run it only on objects registered in
/// <c>qe_managed_source_object</c>; DDL cannot be rolled back, so callers order it before the metadata writes it enables.
/// </summary>
public interface IManagedTables
{
    /// <summary>Creates a table with the given columns.</summary>
    /// <param name="tableName">The new table, a name the database does not have.</param>
    /// <param name="columns">The columns; possibly none.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the table exists.</returns>
    Task CreateAsync(string tableName, IReadOnlyList<DatasetNewColumnInput> columns, CancellationToken cancellationToken);

    /// <summary>Adds columns to a table and returns once StarRocks has finished the schema change.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="columns">The new columns, which the table does not have.</param>
    /// <param name="cancellationToken">Cancels the statement and the wait.</param>
    /// <returns>A task that completes when the columns exist.</returns>
    Task AddColumnsAsync(string tableName, IReadOnlyList<DatasetNewColumnInput> columns, CancellationToken cancellationToken);

    /// <summary>Renames a table.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="newName">Its new name, which the database does not have.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the table is renamed.</returns>
    Task RenameAsync(string tableName, string newName, CancellationToken cancellationToken);
}
