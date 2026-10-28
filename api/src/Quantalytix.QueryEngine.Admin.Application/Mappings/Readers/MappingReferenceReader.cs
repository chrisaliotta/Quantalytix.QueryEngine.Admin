using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Reads what mapping sets and bridges reference or are referenced by, for both kinds' read models: the dimensions they map into
/// with their live versions, the dataset bindings that name them (B13), and the assumption sets that pin their versions.
/// </summary>
public sealed class MappingReferenceReader(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<FieldRecord> fields,
    ITableGateway<AssumptionSetRecord> assumptionSets)
{
    /// <summary>Reads dimensions with their live versions.</summary>
    /// <param name="dimensionIds">The dimensions.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Each dimension's reference, by dimension id.</returns>
    public async Task<IReadOnlyDictionary<long, MappingDimensionRef>> DimensionsAsync(IEnumerable<long> dimensionIds, CancellationToken cancellationToken)
    {
        long[] ids = [.. dimensionIds.Distinct()];
        var live = (await dimensionVersions.ListAsync(new { DimensionId = ids, Status = PublicationStatuses.Published }, cancellationToken))
            .ToDictionary(version => version.DimensionId);
        return (await dimensions.ListAsync(new { DimensionId = ids }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId, dimension => MappingReadModels.DimensionRef(dimension, live.GetValueOrDefault(dimension.DimensionId)));
    }

    /// <summary>
    /// Reads the usage of bindings that name mapping sets or bridges. A bridge binding classifies the field of the binding it remaps,
    /// following its chain of source bindings, and names that binding's role as its source role.
    /// </summary>
    /// <param name="filter">The bindings to read, such as <c>new { MappingSetId = ids }</c>.</param>
    /// <param name="identityOf">The mapping set or bridge a binding names.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The usages by mapping set or bridge id, each ordered by dataset key, version number and role key.</returns>
    public async Task<ILookup<long, MappingBindingUsage>> BindingsAsync(object filter, Func<DimensionBindingRecord, long> identityOf, CancellationToken cancellationToken)
    {
        var bound = await bindings.ListAsync(filter, cancellationToken);
        long[] versionIds = [.. bound.Select(binding => binding.DatasetVersionId).Distinct()];
        var holding = (await datasetVersions.ListAsync(new { DatasetVersionId = versionIds }, cancellationToken)).ToDictionary(version => version.DatasetVersionId);
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = holding.Values.Select(version => version.DatasetId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var versionBindings = (await bindings.ListAsync(new { DatasetVersionId = versionIds }, cancellationToken)).ToDictionary(binding => binding.DimensionBindingId);
        var roleKeys = (await roles.ListAsync(new { DimensionRoleId = versionBindings.Values.Select(binding => binding.DimensionRoleId).OfType<long>().Distinct().ToArray() }, cancellationToken))
            .ToDictionary(role => role.DimensionRoleId, role => role.DimensionRoleKey);
        var fieldKeys = (await fields.ListAsync(new { FieldId = versionBindings.Values.Select(binding => binding.SourceFieldId).OfType<long>().Distinct().ToArray() }, cancellationToken))
            .ToDictionary(field => field.FieldId, field => field.FieldKey);

        long FieldOf(DimensionBindingRecord binding) => binding.SourceFieldId ?? FieldOf(versionBindings[binding.SourceDimensionBindingId!.Value]);

        return bound
            .Select(binding =>
            {
                var version = holding[binding.DatasetVersionId];
                return (Identity: identityOf(binding), Usage: new MappingBindingUsage
                {
                    DatasetId = version.DatasetId,
                    DatasetKey = datasetKeys[version.DatasetId],
                    DatasetVersionId = version.DatasetVersionId,
                    DatasetVersionNumber = version.VersionNumber,
                    Status = version.Status,
                    FieldKey = fieldKeys[FieldOf(binding)],
                    RoleKey = roleKeys[binding.DimensionRoleId!.Value],
                    SourceRoleKey = binding.SourceDimensionBindingId is { } sourceId ? roleKeys[versionBindings[sourceId].DimensionRoleId!.Value] : null
                });
            })
            .OrderBy(entry => entry.Usage.DatasetKey, StringComparer.Ordinal)
            .ThenBy(entry => entry.Usage.DatasetVersionNumber)
            .ThenBy(entry => entry.Usage.RoleKey, StringComparer.Ordinal)
            .ToLookup(entry => entry.Identity, entry => entry.Usage);
    }

    /// <summary>Reads the assumption sets of version pins.</summary>
    /// <param name="pinned">Each pin's assumption set and version.</param>
    /// <param name="versionNumbers">The number of every version of the identity, by version id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pins, the active set first, then by set key.</returns>
    public async Task<IReadOnlyList<MappingAssumptionPin>> PinsAsync(
        IReadOnlyCollection<(long AssumptionSetId, long VersionId)> pinned,
        IReadOnlyDictionary<long, int> versionNumbers,
        CancellationToken cancellationToken)
    {
        var sets = (await assumptionSets.ListAsync(new { AssumptionSetId = pinned.Select(pin => pin.AssumptionSetId).ToArray() }, cancellationToken))
            .ToDictionary(set => set.AssumptionSetId);
        return
        [
            .. pinned
                .Select(pin => new MappingAssumptionPin
                {
                    AssumptionSetId = pin.AssumptionSetId,
                    AssumptionSetKey = sets[pin.AssumptionSetId].AssumptionSetKey,
                    AssumptionSetDisplayName = sets[pin.AssumptionSetId].DisplayName,
                    IsActive = sets[pin.AssumptionSetId].Status == AssumptionSetStatuses.Active,
                    VersionId = pin.VersionId,
                    VersionNumber = versionNumbers[pin.VersionId]
                })
                .OrderByDescending(pin => pin.IsActive)
                .ThenBy(pin => pin.AssumptionSetKey, StringComparer.Ordinal)
        ];
    }
}
