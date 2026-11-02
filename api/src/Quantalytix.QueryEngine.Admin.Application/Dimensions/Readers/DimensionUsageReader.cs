using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Reads what references a dimension (blueprint 12, <c>DimensionDetail.usage</c>): dataset bindings, mapping sets, bridges at
/// either end, calculation dimensions, report scenarios and report sources on its roles, all from the objects of the dimension
/// version's snapshot. Member keys are the ones each object names in that version: unknown members of bindings, entries of mapping
/// versions, mappings of bridge versions, and ancestor groupings of enabled calculation dimensions.
/// </summary>
public sealed class DimensionUsageReader(
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<DimensionMappingRecord> mappingEntries,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ITableGateway<ReportScenarioRecord> scenarios,
    ITableGateway<ReportSourceRecord> sources,
    BindingNames names)
{
    /// <summary>Reads every reference to a dimension from the objects of one snapshot.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="roles">The dimension's roles.</param>
    /// <param name="snapshotId">The snapshot of the dimension's version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The references, by kind in contract order.</returns>
    public async Task<IReadOnlyList<DimensionUsageReference>> ReadAsync(
        long dimensionId,
        IReadOnlyList<DimensionRoleRecord> roles,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        var roleKeys = roles.ToDictionary(role => role.DimensionRoleId, role => role.DimensionRoleKey);
        var roleIds = roleKeys.Keys.ToArray();
        var reportKeys = (await reports.ListAsync(null, cancellationToken)).ToDictionary(report => report.ReportId, report => report.ReportKey);
        var reportOf = (await reportVersions.ListAsync(new { SnapshotId = snapshotId }, cancellationToken)).ToDictionary(version => version.ReportVersionId);
        var held = new { ReportVersionId = reportOf.Keys.ToArray(), DimensionRoleId = roleIds };
        return
        [
            .. await BindingsAsync(dimensionId, roleKeys, snapshotId, cancellationToken),
            .. await MappingSetsAsync(dimensionId, snapshotId, cancellationToken),
            .. await BridgesAsync(dimensionId, snapshotId, cancellationToken),
            .. (await calculationDimensions.ListAsync(held, cancellationToken)).Select(dimension =>
            {
                var report = reportOf[dimension.ReportVersionId];
                var named = report.IsEnabled && dimension.IsEnabled && dimension.GroupingAncestorMemberKey is { } ancestor ? new[] { ancestor } : null;
                return new DimensionUsageReference
                {
                    Kind = DimensionUsageKinds.CalculationDimension,
                    Name = dimension.DimensionKey,
                    Detail = $"of report '{reportKeys[report.ReportId]}'",
                    RoleKey = roleKeys[dimension.DimensionRoleId],
                    MemberKeys = named
                };
            }),
            .. (await scenarios.ListAsync(held, cancellationToken)).Select(scenario => new DimensionUsageReference
            {
                Kind = DimensionUsageKinds.ReportScenario,
                Name = reportKeys[reportOf[scenario.ReportVersionId].ReportId],
                Detail = reportOf[scenario.ReportVersionId].DisplayName,
                RoleKey = roleKeys[scenario.DimensionRoleId]
            }),
            .. (await sources.ListAsync(held, cancellationToken)).Select(source => new DimensionUsageReference
            {
                Kind = DimensionUsageKinds.ReportSource,
                Name = source.SourceKey,
                Detail = $"of report '{reportKeys[reportOf[source.ReportVersionId].ReportId]}'",
                RoleKey = roleKeys[source.DimensionRoleId!.Value]
            })
        ];
    }

    /// <summary>The bindings of the snapshot's dataset versions, with their unknown member keys.</summary>
    private async Task<IEnumerable<DimensionUsageReference>> BindingsAsync(
        long dimensionId,
        IReadOnlyDictionary<long, string> roleKeys,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        var holding = (await datasetVersions.ListAsync(new { SnapshotId = snapshotId }, cancellationToken)).ToDictionary(version => version.DatasetVersionId);
        var bound = await bindings.ListAsync(new { DimensionId = dimensionId, DatasetVersionId = holding.Keys.ToArray() }, cancellationToken);
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = holding.Values.Select(version => version.DatasetId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var named = await names.OfAsync(bound, cancellationToken);
        return bound.Select(binding =>
        {
            var version = holding[binding.DatasetVersionId];
            var datasetKey = datasetKeys[version.DatasetId];
            return new DimensionUsageReference
            {
                Kind = DimensionUsageKinds.DatasetBinding,
                Name = datasetKey,
                Detail = $"binding '{named[binding.DimensionBindingId]}'",
                RoleKey = binding.DimensionRoleId is { } roleId ? roleKeys[roleId] : null,
                MemberKeys = binding.UnknownMemberKey is { } key ? [key] : null
            };
        });
    }

    /// <summary>The mapping sets that target the dimension, with the member keys of their snapshot version's entries.</summary>
    private async Task<IEnumerable<DimensionUsageReference>> MappingSetsAsync(long dimensionId, long snapshotId, CancellationToken cancellationToken)
    {
        var sets = await mappingSets.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        var held = await mappingVersions.ListAsync(
            new { MappingSetId = sets.Select(set => set.MappingSetId).ToArray(), SnapshotId = snapshotId }, cancellationToken);
        var entries = (await mappingEntries.ListAsync(new { MappingVersionId = held.Select(version => version.MappingVersionId).ToArray() }, cancellationToken))
            .ToLookup(entry => entry.MappingVersionId, entry => entry.MemberKey);
        var liveOf = held.ToDictionary(version => version.MappingSetId, version => version.MappingVersionId);
        return sets.Select(set => new DimensionUsageReference
        {
            Kind = DimensionUsageKinds.MappingSet,
            Name = set.MappingSetKey,
            Detail = set.DisplayName,
            MemberKeys = liveOf.TryGetValue(set.MappingSetId, out var versionId) ? Keys(entries[versionId]) : null
        });
    }

    /// <summary>The bridges from or to the dimension, with the member keys their snapshot version's mappings name on its side.</summary>
    private async Task<IEnumerable<DimensionUsageReference>> BridgesAsync(long dimensionId, long snapshotId, CancellationToken cancellationToken)
    {
        var from = await bridges.ListAsync(new { SourceDimensionId = dimensionId }, cancellationToken);
        var to = await bridges.ListAsync(new { TargetDimensionId = dimensionId }, cancellationToken);
        var joined = from.Concat(to).DistinctBy(bridge => bridge.BridgeId).ToList();
        var held = await bridgeVersions.ListAsync(
            new { BridgeId = joined.Select(bridge => bridge.BridgeId).ToArray(), SnapshotId = snapshotId }, cancellationToken);
        var mappings = (await bridgeMappings.ListAsync(new { BridgeVersionId = held.Select(version => version.BridgeVersionId).ToArray() }, cancellationToken))
            .ToLookup(mapping => mapping.BridgeVersionId);
        var liveOf = held.ToDictionary(version => version.BridgeId, version => version.BridgeVersionId);
        return joined.Select(bridge => new DimensionUsageReference
        {
            Kind = DimensionUsageKinds.Bridge,
            Name = bridge.BridgeKey,
            Detail = bridge.DisplayName,
            MemberKeys = liveOf.TryGetValue(bridge.BridgeId, out var versionId)
                ? Keys(mappings[versionId].SelectMany(mapping => new[]
                {
                    bridge.SourceDimensionId == dimensionId ? mapping.SourceMemberKey : null,
                    bridge.TargetDimensionId == dimensionId ? mapping.TargetMemberKey : null
                }.OfType<string>()))
                : null
        });
    }

    /// <summary>Distinct keys in ordinal order, or null when there are none.</summary>
    private static string[]? Keys(IEnumerable<string> keys)
    {
        string[] distinct = [.. keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return distinct.Length == 0 ? null : distinct;
    }
}
