using System.Globalization;
using Dapper;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.MaterializedViews;

/// <summary>
/// The asynchronous materialized views of the request tenant's database. Their status comes from
/// <c>information_schema.materialized_views</c> and their creation time from <c>information_schema.tables</c>, read separately because
/// StarRocks plans joins across <c>information_schema</c> poorly.
/// </summary>
public sealed class StarRocksMaterializedViews(IStarRocksConnectionFactory connections) : IMaterializedViews
{
    /// <summary>The refresh status of every materialized view of the connection's database.</summary>
    private const string StatusSql = """
        SELECT table_name, is_active, inactive_reason, last_refresh_state, last_refresh_finished_time, last_refresh_error_message
        FROM information_schema.materialized_views
        WHERE table_schema = DATABASE()
        """;

    /// <summary>The creation time of the named objects of the connection's database.</summary>
    private const string CreationSql = """
        SELECT table_name, create_time
        FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_name IN @Names
        """;

    /// <summary>The <c>is_active</c> value of a maintained view.</summary>
    private const string Active = "true";

    /// <summary>The <c>last_refresh_state</c> of a refresh that succeeded.</summary>
    private const string Success = "SUCCESS";

    /// <summary>The <c>last_refresh_state</c> of a refresh that failed.</summary>
    private const string Failed = "FAILED";

    /// <summary>How StarRocks writes a refresh time.</summary>
    private const string RefreshTimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>Reads the status of every materialized view.</summary>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>Each view's status, by name.</returns>
    public async Task<IReadOnlyDictionary<string, MaterializedViewStatus>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        var views = (await connection.QueryAsync<InformationSchemaMaterializedView>(new CommandDefinition(StatusSql, cancellationToken: cancellationToken))).ToList();
        if (views.Count == 0) return new Dictionary<string, MaterializedViewStatus>();
        var created = (await connection.QueryAsync<InformationSchemaViewCreation>(new CommandDefinition(CreationSql, new { Names = views.Select(view => view.TableName).ToArray() }, cancellationToken: cancellationToken)))
            .ToDictionary(row => row.TableName, row => row.CreateTime, StringComparer.Ordinal);
        return views.ToDictionary(view => view.TableName, view => StatusOf(view, created[view.TableName]), StringComparer.Ordinal);
    }

    /// <summary>Drops the view when it exists and creates it with the definition's columns; StarRocks then refreshes it.</summary>
    /// <param name="definition">The view.</param>
    /// <param name="cancellationToken">Cancels the statements.</param>
    /// <returns>A task that completes when the view is defined.</returns>
    public async Task DefineAsync(MaterializedViewDefinition definition, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(MaterializedViewSql.Drop(definition.ViewName), cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(MaterializedViewSql.Create(definition), cancellationToken: cancellationToken));
    }

    /// <summary>Drops a view.</summary>
    /// <param name="viewName">The view.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the view is gone.</returns>
    public async Task DropAsync(string viewName, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(MaterializedViewSql.Drop(viewName), cancellationToken: cancellationToken));
    }

    /// <summary>Starts a refresh of a view.</summary>
    /// <param name="viewName">The view.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the refresh is queued.</returns>
    public async Task RefreshAsync(string viewName, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(MaterializedViewSql.Refresh(viewName), cancellationToken: cancellationToken));
    }

    /// <summary>The application's status of a view: failed when inactive or its last refresh failed, ready after a success, building otherwise.</summary>
    internal static MaterializedViewStatus StatusOf(InformationSchemaMaterializedView view, DateTime createdAt)
    {
        var finished = DateTime.TryParseExact(view.LastRefreshFinishedTime, RefreshTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? at
            : (DateTime?)null;
        if (view.IsActive != Active)
            return new MaterializedViewStatus { State = MaterializationStates.Failed, DefinedAt = createdAt, RefreshedAt = finished, Error = view.InactiveReason };
        return view.LastRefreshState switch
        {
            Success => new MaterializedViewStatus { State = MaterializationStates.Ready, DefinedAt = createdAt, RefreshedAt = finished, Error = null },
            Failed => new MaterializedViewStatus { State = MaterializationStates.Failed, DefinedAt = createdAt, RefreshedAt = finished, Error = view.LastRefreshErrorMessage },
            _ => new MaterializedViewStatus { State = MaterializationStates.Building, DefinedAt = createdAt, RefreshedAt = null, Error = null }
        };
    }
}
