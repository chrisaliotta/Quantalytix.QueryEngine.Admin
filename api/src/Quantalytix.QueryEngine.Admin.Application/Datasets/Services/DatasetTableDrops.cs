using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The managed tables a dataset delete takes with it (OI-33): every table registered in <c>qe_managed_source_object</c> that the
/// dataset's versions read and no version of another dataset reads. Each is dropped as <see cref="WritePlan"/> schema statements,
/// its materialized view (OI-32) before the table the view reads, and its registry row is deleted with the dataset's rows. The DDL
/// is planned from what the database still holds, so a replay after a delete that stopped midway drops nothing twice.
/// </summary>
/// <param name="versions">The dataset versions, to find other readers of a table.</param>
/// <param name="managed">The managed-table registry.</param>
/// <param name="sourceObjects">The tables the database holds.</param>
/// <param name="managedTables">Drops the tables.</param>
/// <param name="views">Drops the materialized views.</param>
public sealed class DatasetTableDrops(
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<ManagedSourceObjectRecord> managed,
    SourceObjectDirectory sourceObjects,
    IManagedTables managedTables,
    IMaterializedViews views)
{
    /// <summary>Adds the drops of the managed tables only the deleted dataset reads, and the deletes of their registry rows.</summary>
    /// <param name="datasetId">The dataset being deleted.</param>
    /// <param name="deleted">Its versions.</param>
    /// <param name="plan">The delete's plan.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the writes are planned.</returns>
    public async Task PlanAsync(long datasetId, IReadOnlyList<DatasetVersionRecord> deleted, WritePlan plan, CancellationToken cancellationToken)
    {
        var dropped = await DroppedAsync(datasetId, deleted, cancellationToken);
        if (dropped.Count == 0) return;
        var existing = (await sourceObjects.ObjectsAsync(cancellationToken)).Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        var defined = await views.ListAsync(cancellationToken);
        foreach (var table in dropped.Select(row => row.SourceObject))
        {
            var viewName = MaterializedViewNames.ViewOf(table);
            if (defined.ContainsKey(viewName)) plan.Schema(token => views.DropAsync(viewName, token));
            if (existing.Contains(table)) plan.Schema(token => managedTables.DropAsync(table, token));
        }

        plan.Delete(managed, 1, new { ManagedSourceObjectId = dropped.Select(row => row.ManagedSourceObjectId).ToArray() });
    }

    /// <summary>The registry rows of the managed tables the deleted versions read that no version of another dataset reads.</summary>
    private async Task<List<ManagedSourceObjectRecord>> DroppedAsync(long datasetId, IReadOnlyList<DatasetVersionRecord> deleted, CancellationToken cancellationToken)
    {
        var read = (await managed.ListAsync(null, cancellationToken))
            .Where(row => deleted.Any(version => version.SourceObjectKind == row.SourceObjectKind && version.SourceObject == row.SourceObject))
            .ToList();
        if (read.Count == 0) return read;
        var readers = await versions.ListAsync(new { SourceObject = read.Select(row => row.SourceObject).ToArray() }, cancellationToken);
        return
        [
            .. read.Where(row => !readers.Any(version =>
                version.DatasetId != datasetId && version.SourceObjectKind == row.SourceObjectKind && version.SourceObject == row.SourceObject))
        ];
    }
}
