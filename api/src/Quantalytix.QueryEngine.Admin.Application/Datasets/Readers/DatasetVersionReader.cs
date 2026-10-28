using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Builds a version's read model from its snapshot, reading the materialized views' status only when a field names a view column.</summary>
/// <param name="views">Reads the materialized views' status.</param>
public sealed class DatasetVersionReader(IMaterializedViews views)
{
    /// <summary>The version content of a snapshot.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <param name="cancellationToken">Cancels the status read.</param>
    /// <returns>The content.</returns>
    public async Task<DatasetVersion> ReadAsync(DatasetSnapshot snapshot, Func<long, string> presentId, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, MaterializedViewStatus> statuses = snapshot.Fields.Any(field => field.MaterializedColumn is not null)
            ? await views.ListAsync(cancellationToken)
            : new Dictionary<string, MaterializedViewStatus>();
        return DatasetReadModels.Version(snapshot, statuses, presentId);
    }
}
