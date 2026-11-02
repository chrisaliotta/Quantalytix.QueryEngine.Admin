using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Reads a dataset version aggregate as stored, with what surrounds it: the live snapshot, the tenant's dataset versions, the
/// dimension catalog its bindings name with the identities that have a version in the version's own snapshot, the managed-table
/// registry and the physical tables, views and columns.
/// </summary>
public sealed class DatasetSnapshotLoader(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    DatasetVersionContentStore content,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    LiveSnapshot live,
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
        var held = new { version.SnapshotId };
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
                LiveSnapshotId = await live.IdAsync(cancellationToken),
                TenantVersions = await versions.ListAsync(null, cancellationToken),
                Roles = await roles.ListAsync(null, cancellationToken),
                Dimensions = await dimensions.ListAsync(null, cancellationToken),
                MappingSets = await mappingSets.ListAsync(null, cancellationToken),
                Bridges = await bridges.ListAsync(null, cancellationToken),
                HeldDimensionIds = (await dimensionVersions.ListAsync(held, cancellationToken)).Select(row => row.DimensionId).ToHashSet(),
                HeldMappingSetIds = (await mappingVersions.ListAsync(held, cancellationToken)).Select(row => row.MappingSetId).ToHashSet(),
                HeldBridgeIds = (await bridgeVersions.ListAsync(held, cancellationToken)).Select(row => row.BridgeId).ToHashSet(),
                SourceObjects = await sourceObjects.ObjectsAsync(cancellationToken),
                Columns = await sourceObjects.AllColumnsAsync(cancellationToken)
            }
        };
    }
}
