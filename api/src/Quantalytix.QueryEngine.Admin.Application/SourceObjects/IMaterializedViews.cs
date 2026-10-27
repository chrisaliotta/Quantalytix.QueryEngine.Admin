namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>
/// The StarRocks asynchronous materialized views that hold the extracted values of JSON path fields, one per source object
/// (blueprint 11, OI-32). Their refresh status is read from StarRocks, so no state table mirrors it.
/// </summary>
public interface IMaterializedViews
{
    /// <summary>Reads the status of every materialized view of the tenant database.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The views' statuses, by view name.</returns>
    Task<IReadOnlyDictionary<string, MaterializedViewStatus>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Replaces a view with one of the given columns, which StarRocks then refreshes asynchronously.</summary>
    /// <param name="definition">The view, its source object and the extracted columns.</param>
    /// <param name="cancellationToken">Cancels the statements.</param>
    /// <returns>A task that completes when the view is defined; its refresh continues in StarRocks.</returns>
    Task DefineAsync(MaterializedViewDefinition definition, CancellationToken cancellationToken);

    /// <summary>Drops a view.</summary>
    /// <param name="viewName">The view.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the view is gone.</returns>
    Task DropAsync(string viewName, CancellationToken cancellationToken);

    /// <summary>Starts a refresh of a view.</summary>
    /// <param name="viewName">The view.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when StarRocks has queued the refresh.</returns>
    Task RefreshAsync(string viewName, CancellationToken cancellationToken);
}
