using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Reads what references a dimension (blueprint 12, <c>DimensionDetail.usage</c>): dataset bindings, mapping sets, bridges at
/// either end, calculation dimensions, report scenarios and report sources on its roles, and assumption set pins of its versions.
/// Member keys are the ones each object names in the live version: unknown members of published bindings, entries of published
/// mapping versions, mappings of published bridge versions, and ancestor groupings of enabled calculation dimensions.
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
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ITableGateway<ReportScenarioRecord> scenarios,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<AssumptionSetRecord> assumptionSets,
    ITableGateway<AssumptionSetDimensionRecord> pins,
    BindingNames names)
{
    /// <summary>Reads every reference to a dimension.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="roles">The dimension's roles.</param>
    /// <param name="versions">The dimension's versions.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The references, by kind in contract order.</returns>
    public async Task<IReadOnlyList<DimensionUsageReference>> ReadAsync(
        long dimensionId,
        IReadOnlyList<DimensionRoleRecord> roles,
        IReadOnlyList<DimensionVersionRecord> versions,
        CancellationToken cancellationToken)
    {
        var roleKeys = roles.ToDictionary(role => role.DimensionRoleId, role => role.DimensionRoleKey);
        var roleIds = roleKeys.Keys.ToArray();
        var reportsById = (await reports.ListAsync(null, cancellationToken)).ToDictionary(report => report.ReportId);
        return
        [
            .. await BindingsAsync(dimensionId, roleKeys, cancellationToken),
            .. await MappingSetsAsync(dimensionId, cancellationToken),
            .. await BridgesAsync(dimensionId, cancellationToken),
            .. (await calculationDimensions.ListAsync(new { DimensionRoleId = roleIds }, cancellationToken)).Select(dimension =>
            {
                var report = reportsById[dimension.ReportId];
                var named = report.IsEnabled && dimension.IsEnabled && dimension.GroupingAncestorMemberKey is { } ancestor ? new[] { ancestor } : null;
                return new DimensionUsageReference
                {
                    Kind = DimensionUsageKinds.CalculationDimension,
                    Name = dimension.DimensionKey,
                    Detail = $"of report '{report.ReportKey}'",
                    RoleKey = roleKeys[dimension.DimensionRoleId],
                    MemberKeys = named
                };
            }),
            .. (await scenarios.ListAsync(new { DimensionRoleId = roleIds }, cancellationToken)).Select(scenario => new DimensionUsageReference
            {
                Kind = DimensionUsageKinds.ReportScenario,
                Name = reportsById[scenario.ReportId].ReportKey,
                Detail = reportsById[scenario.ReportId].DisplayName,
                RoleKey = roleKeys[scenario.DimensionRoleId]
            }),
            .. (await sources.ListAsync(new { DimensionRoleId = roleIds }, cancellationToken)).Select(source => new DimensionUsageReference
            {
                Kind = DimensionUsageKinds.ReportSource,
                Name = source.SourceKey,
                Detail = $"of report '{reportsById[source.ReportId].ReportKey}'",
                RoleKey = roleKeys[source.DimensionRoleId!.Value]
            }),
            .. await PinsAsync(dimensionId, versions, cancellationToken)
        ];
    }

    /// <summary>The bindings of every dataset version, with the unknown member key of a published one.</summary>
    private async Task<IEnumerable<DimensionUsageReference>> BindingsAsync(
        long dimensionId,
        IReadOnlyDictionary<long, string> roleKeys,
        CancellationToken cancellationToken)
    {
        var bound = await bindings.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        var holding = (await datasetVersions.ListAsync(new { DatasetVersionId = bound.Select(binding => binding.DatasetVersionId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(version => version.DatasetVersionId);
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = holding.Values.Select(version => version.DatasetId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var named = await names.OfAsync(bound, cancellationToken);
        return bound.Select(binding =>
        {
            var version = holding[binding.DatasetVersionId];
            var datasetKey = datasetKeys[version.DatasetId];
            var unknown = version.Status == PublicationStatuses.Published && binding.UnknownMemberKey is { } key ? new[] { key } : null;
            return new DimensionUsageReference
            {
                Kind = DimensionUsageKinds.DatasetBinding,
                Name = datasetKey,
                Detail = $"binding '{named[binding.DimensionBindingId]}' of v{version.VersionNumber}",
                RoleKey = binding.DimensionRoleId is { } roleId ? roleKeys[roleId] : null,
                MemberKeys = unknown,
                Label = $"'{datasetKey}' v{version.VersionNumber}"
            };
        });
    }

    /// <summary>The mapping sets that target the dimension, with the member keys of their published version's entries.</summary>
    private async Task<IEnumerable<DimensionUsageReference>> MappingSetsAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var sets = await mappingSets.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        var published = await mappingVersions.ListAsync(
            new { MappingSetId = sets.Select(set => set.MappingSetId).ToArray(), Status = PublicationStatuses.Published }, cancellationToken);
        var entries = (await mappingEntries.ListAsync(new { MappingVersionId = published.Select(version => version.MappingVersionId).ToArray() }, cancellationToken))
            .ToLookup(entry => entry.MappingVersionId, entry => entry.MemberKey);
        var liveOf = published.ToDictionary(version => version.MappingSetId, version => version.MappingVersionId);
        return sets.Select(set => new DimensionUsageReference
        {
            Kind = DimensionUsageKinds.MappingSet,
            Name = set.MappingSetKey,
            Detail = set.DisplayName,
            MemberKeys = liveOf.TryGetValue(set.MappingSetId, out var versionId) ? Keys(entries[versionId]) : null
        });
    }

    /// <summary>The bridges from or to the dimension, with the member keys their published version's mappings name on its side.</summary>
    private async Task<IEnumerable<DimensionUsageReference>> BridgesAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var from = await bridges.ListAsync(new { SourceDimensionId = dimensionId }, cancellationToken);
        var to = await bridges.ListAsync(new { TargetDimensionId = dimensionId }, cancellationToken);
        var joined = from.Concat(to).DistinctBy(bridge => bridge.BridgeId).ToList();
        var published = await bridgeVersions.ListAsync(
            new { BridgeId = joined.Select(bridge => bridge.BridgeId).ToArray(), Status = PublicationStatuses.Published }, cancellationToken);
        var mappings = (await bridgeMappings.ListAsync(new { BridgeVersionId = published.Select(version => version.BridgeVersionId).ToArray() }, cancellationToken))
            .ToLookup(mapping => mapping.BridgeVersionId);
        var liveOf = published.ToDictionary(version => version.BridgeId, version => version.BridgeVersionId);
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

    /// <summary>The assumption sets that pin a version of the dimension.</summary>
    private async Task<IEnumerable<DimensionUsageReference>> PinsAsync(
        long dimensionId,
        IReadOnlyList<DimensionVersionRecord> versions,
        CancellationToken cancellationToken)
    {
        var pinned = await pins.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        var sets = (await assumptionSets.ListAsync(new { AssumptionSetId = pinned.Select(pin => pin.AssumptionSetId).ToArray() }, cancellationToken))
            .ToDictionary(set => set.AssumptionSetId);
        var numbers = versions.ToDictionary(version => version.DimensionVersionId, version => version.VersionNumber);
        return pinned.Select(pin =>
        {
            var set = sets[pin.AssumptionSetId];
            var number = numbers[pin.DimensionVersionId];
            return new DimensionUsageReference
            {
                Kind = DimensionUsageKinds.AssumptionSet,
                Name = set.AssumptionSetKey,
                Detail = $"{set.Status} → v{number}",
                PinsVersion = number,
                IsHistoricalPin = set.Status == AssumptionSetStatuses.Historical
            };
        });
    }

    /// <summary>Distinct keys in ordinal order, or null when there are none.</summary>
    private static string[]? Keys(IEnumerable<string> keys)
    {
        string[] distinct = [.. keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return distinct.Length == 0 ? null : distinct;
    }
}
