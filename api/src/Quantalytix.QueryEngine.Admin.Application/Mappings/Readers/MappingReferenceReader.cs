using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Reads what mapping sets and bridges reference or are referenced by in one snapshot, for both kinds' read models: the dimensions
/// they map into with their versions, and the dataset bindings that name them (B13).
/// </summary>
public sealed class MappingReferenceReader(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<FieldRecord> fields)
{
    /// <summary>Reads dimensions with their versions in a snapshot.</summary>
    /// <param name="dimensionIds">The dimensions.</param>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Each dimension's reference, by dimension id.</returns>
    public async Task<IReadOnlyDictionary<long, MappingDimensionRef>> DimensionsAsync(IEnumerable<long> dimensionIds, long snapshotId, CancellationToken cancellationToken)
    {
        long[] ids = [.. dimensionIds.Distinct()];
        var held = (await dimensionVersions.ListAsync(new { DimensionId = ids, SnapshotId = snapshotId }, cancellationToken))
            .ToDictionary(version => version.DimensionId);
        return (await dimensions.ListAsync(new { DimensionId = ids }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId, dimension => MappingReadModels.DimensionRef(dimension, held.GetValueOrDefault(dimension.DimensionId)));
    }

    /// <summary>
    /// Reads the usage of the bindings of a snapshot's dataset versions that name mapping sets or bridges. A bridge binding classifies
    /// the field of the binding it remaps, following its chain of source bindings, and names that binding's role as its source role.
    /// </summary>
    /// <param name="bound">Selects the bindings that name the identities among a snapshot's bindings.</param>
    /// <param name="identityOf">The mapping set or bridge a binding names.</param>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The usages by mapping set or bridge id, each ordered by dataset key and role key.</returns>
    public async Task<ILookup<long, MappingBindingUsage>> BindingsAsync(
        Func<DimensionBindingRecord, bool> bound,
        Func<DimensionBindingRecord, long> identityOf,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        var holding = (await datasetVersions.ListAsync(new { SnapshotId = snapshotId }, cancellationToken)).ToDictionary(version => version.DatasetVersionId);
        var snapshotBindings = await bindings.ListAsync(new { DatasetVersionId = holding.Keys.ToArray() }, cancellationToken);
        var naming = snapshotBindings.Where(bound).ToList();
        long[] versionIds = [.. naming.Select(binding => binding.DatasetVersionId).Distinct()];
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = holding.Values.Select(version => version.DatasetId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var versionBindings = snapshotBindings.Where(binding => versionIds.Contains(binding.DatasetVersionId)).ToDictionary(binding => binding.DimensionBindingId);
        var roleKeys = (await roles.ListAsync(new { DimensionRoleId = versionBindings.Values.Select(binding => binding.DimensionRoleId).OfType<long>().Distinct().ToArray() }, cancellationToken))
            .ToDictionary(role => role.DimensionRoleId, role => role.DimensionRoleKey);
        var fieldKeys = (await fields.ListAsync(new { FieldId = versionBindings.Values.Select(binding => binding.SourceFieldId).OfType<long>().Distinct().ToArray() }, cancellationToken))
            .ToDictionary(field => field.FieldId, field => field.FieldKey);

        long FieldOf(DimensionBindingRecord binding) => binding.SourceFieldId ?? FieldOf(versionBindings[binding.SourceDimensionBindingId!.Value]);

        return naming
            .Select(binding =>
            {
                var version = holding[binding.DatasetVersionId];
                return (Identity: identityOf(binding), Usage: new MappingBindingUsage
                {
                    DatasetId = version.DatasetId,
                    DatasetKey = datasetKeys[version.DatasetId],
                    DatasetVersionId = version.DatasetVersionId,
                    FieldKey = fieldKeys[FieldOf(binding)],
                    RoleKey = roleKeys[binding.DimensionRoleId!.Value],
                    SourceRoleKey = binding.SourceDimensionBindingId is { } sourceId ? roleKeys[versionBindings[sourceId].DimensionRoleId!.Value] : null
                });
            })
            .OrderBy(entry => entry.Usage.DatasetKey, StringComparer.Ordinal)
            .ThenBy(entry => entry.Usage.RoleKey, StringComparer.Ordinal)
            .ToLookup(entry => entry.Identity, entry => entry.Usage);
    }
}
