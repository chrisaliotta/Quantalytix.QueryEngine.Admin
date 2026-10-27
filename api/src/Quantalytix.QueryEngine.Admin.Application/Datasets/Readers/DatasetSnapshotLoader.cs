using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Reads a dataset version aggregate as stored, with what surrounds it: the tenant's dataset versions, the dataset's release
/// pins, the dimension catalog its bindings name, the managed-table registry and the physical tables, views and columns.
/// </summary>
public sealed class DatasetSnapshotLoader(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    DatasetVersionContentStore content,
    DatasetReleaseReader releases,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<BridgeRecord> bridges,
    SourceObjectDirectory sourceObjects)
{
    /// <summary>Reads a version with its dataset and surroundings.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<DatasetSnapshot> LoadAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var rows = await content.LoadAsync([versionId], cancellationToken);
        return new DatasetSnapshot
        {
            Dataset = await datasets.GetAsync(new { version.DatasetId }, cancellationToken),
            Version = version,
            Fields = rows.Fields,
            Grains = rows.Grains,
            GrainFields = rows.GrainFields,
            Bindings = rows.Bindings,
            TimeBindings = rows.TimeBindings,
            Measures = rows.Measures,
            Components = rows.Components,
            Dependencies = rows.Dependencies,
            TimePolicies = rows.TimePolicies,
            ManagedObjects = await sourceObjects.ManagedAsync(cancellationToken),
            Surroundings = new DatasetSurroundings
            {
                TenantVersions = await versions.ListAsync(null, cancellationToken),
                Releases = await releases.ReadAsync(version.DatasetId, cancellationToken),
                Roles = await roles.ListAsync(null, cancellationToken),
                Dimensions = await dimensions.ListAsync(null, cancellationToken),
                MappingSets = await mappingSets.ListAsync(null, cancellationToken),
                Bridges = await bridges.ListAsync(null, cancellationToken),
                SourceObjects = await sourceObjects.ObjectsAsync(cancellationToken),
                Columns = await sourceObjects.AllColumnsAsync(cancellationToken)
            }
        };
    }
}
