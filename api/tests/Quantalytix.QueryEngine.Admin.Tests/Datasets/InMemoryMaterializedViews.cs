using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>An <see cref="IMaterializedViews"/> over in-memory definitions; a defined view is building until a test finishes it.</summary>
internal sealed class InMemoryMaterializedViews : IMaterializedViews
{
    /// <summary>The definition of each view, by name.</summary>
    public Dictionary<string, MaterializedViewDefinition> Definitions { get; } = new(StringComparer.Ordinal);

    /// <summary>The status of each view, by name.</summary>
    public Dictionary<string, MaterializedViewStatus> Statuses { get; } = new(StringComparer.Ordinal);

    /// <summary>The views refreshed, in order.</summary>
    public List<string> Refreshed { get; } = [];

    /// <summary>Returns the statuses.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>The statuses by view name.</returns>
    public Task<IReadOnlyDictionary<string, MaterializedViewStatus>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyDictionary<string, MaterializedViewStatus>>(new Dictionary<string, MaterializedViewStatus>(Statuses));
    }

    /// <summary>Stores the definition, building.</summary>
    /// <param name="definition">The view.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task DefineAsync(MaterializedViewDefinition definition, CancellationToken cancellationToken)
    {
        Definitions[definition.ViewName] = definition;
        Statuses[definition.ViewName] = new MaterializedViewStatus { State = MaterializationStates.Building, DefinedAt = DateTime.UnixEpoch, RefreshedAt = null, Error = null };
        return Task.CompletedTask;
    }

    /// <summary>Removes the view.</summary>
    /// <param name="viewName">The view.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task DropAsync(string viewName, CancellationToken cancellationToken)
    {
        Definitions.Remove(viewName);
        Statuses.Remove(viewName);
        return Task.CompletedTask;
    }

    /// <summary>Records the refresh.</summary>
    /// <param name="viewName">The view.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task RefreshAsync(string viewName, CancellationToken cancellationToken)
    {
        Refreshed.Add(viewName);
        return Task.CompletedTask;
    }
}
