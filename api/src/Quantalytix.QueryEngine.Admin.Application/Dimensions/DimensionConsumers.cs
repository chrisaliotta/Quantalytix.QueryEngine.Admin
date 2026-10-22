using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Lists the level and member keys that live objects name in a dimension: the unknown members of bindings of published dataset
/// versions, entries of published mapping versions, mappings of published bridge versions, and the groupings of the enabled
/// calculation dimensions of enabled reports on the dimension's roles. They resolve the keys in the dimension's published version,
/// so publishing another version or deleting from the published one must keep the keys. A report's own rows name keys of its
/// report dimension, which only the report editor changes, and it keeps them resolved.
/// </summary>
public sealed class DimensionConsumers(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<DimensionMappingRecord> mappingEntries,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    ITableGateway<ReportRecord> reports,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    BindingNames names)
{
    /// <summary>Lists every key the live objects name in a dimension.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The key references, by consumer kind.</returns>
    public async Task<IReadOnlyList<KeyReference>> ListAsync(long dimensionId, CancellationToken cancellationToken)
    {
        return
        [
            .. await BindingsAsync(dimensionId, cancellationToken),
            .. await MappingEntriesAsync(dimensionId, cancellationToken),
            .. await BridgeMappingsAsync(dimensionId, cancellationToken),
            .. await ReportsAsync(dimensionId, cancellationToken)
        ];
    }

    /// <summary>The unknown member keys of the bindings of published dataset versions.</summary>
    private async Task<IEnumerable<KeyReference>> BindingsAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var published = await datasetVersions.ListAsync(new { Status = PublicationStatuses.Published }, cancellationToken);
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = published.Select(version => version.DatasetId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var datasetOf = published.ToDictionary(version => version.DatasetVersionId, version => datasetKeys[version.DatasetId]);
        var routing = (await bindings.ListAsync(new { DimensionId = dimensionId, DatasetVersionId = datasetOf.Keys.ToArray() }, cancellationToken))
            .Where(binding => binding.UnknownMemberKey is not null)
            .ToList();
        var named = await names.OfAsync(routing, cancellationToken);
        return routing.Select(binding =>
            KeyReference.Member(binding.UnknownMemberKey!, $"Binding '{named[binding.BindingId]}' of dataset '{datasetOf[binding.DatasetVersionId]}'"));
    }

    /// <summary>The member keys of the entries of the published versions of the dimension's mapping sets.</summary>
    private async Task<IEnumerable<KeyReference>> MappingEntriesAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var setKeys = (await mappingSets.ListAsync(new { DimensionId = dimensionId }, cancellationToken))
            .ToDictionary(set => set.MappingSetId, set => set.MappingSetKey);
        var published = await mappingVersions.ListAsync(
            new { MappingSetId = setKeys.Keys.ToArray(), Status = PublicationStatuses.Published }, cancellationToken);
        var setOf = published.ToDictionary(version => version.MappingVersionId, version => setKeys[version.MappingSetId]);
        var entries = await mappingEntries.ListAsync(new { MappingVersionId = setOf.Keys.ToArray() }, cancellationToken);
        return entries.Select(entry =>
            KeyReference.Member(entry.MemberKey, $"Source key '{entry.SourceKeyValue}' of mapping set '{setOf[entry.MappingVersionId]}'"));
    }

    /// <summary>The source or target member keys of the mappings of the published versions of the bridges from or to the dimension.</summary>
    private async Task<IEnumerable<KeyReference>> BridgeMappingsAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var from = await bridges.ListAsync(new { SourceDimensionId = dimensionId }, cancellationToken);
        var to = await bridges.ListAsync(new { TargetDimensionId = dimensionId }, cancellationToken);
        var endpoints = from.Concat(to).DistinctBy(bridge => bridge.BridgeId).ToDictionary(bridge => bridge.BridgeId);
        var published = await bridgeVersions.ListAsync(
            new { BridgeId = endpoints.Keys.ToArray(), Status = PublicationStatuses.Published }, cancellationToken);
        var bridgeOf = published.ToDictionary(version => version.BridgeVersionId, version => endpoints[version.BridgeId]);
        var mappings = await bridgeMappings.ListAsync(new { BridgeVersionId = bridgeOf.Keys.ToArray() }, cancellationToken);
        return mappings.SelectMany(mapping =>
        {
            var bridge = bridgeOf[mapping.BridgeVersionId];
            var consumer = $"Bridge '{bridge.BridgeKey}'";
            return new[]
            {
                bridge.SourceDimensionId == dimensionId ? KeyReference.Member(mapping.SourceMemberKey, consumer) : null,
                bridge.TargetDimensionId == dimensionId ? KeyReference.Member(mapping.TargetMemberKey, consumer) : null
            }.OfType<KeyReference>();
        });
    }

    /// <summary>The grouping keys of the enabled calculation dimensions of enabled reports on the dimension's roles.</summary>
    private async Task<IEnumerable<KeyReference>> ReportsAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var enabled = (await reports.ListAsync(null, cancellationToken)).Where(report => report.IsEnabled).ToDictionary(report => report.ReportId, report => report.ReportKey);
        var roleIds = (await roles.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).Select(role => role.RoleId).ToArray();
        var grouping = (await calculationDimensions.ListAsync(new { ReportId = enabled.Keys.ToArray(), RoleId = roleIds }, cancellationToken))
            .Where(dimension => dimension.IsEnabled);
        return grouping.SelectMany(dimension =>
        {
            var consumer = $"Calculation dimension '{dimension.DimensionKey}' of report '{enabled[dimension.ReportId]}'";
            return new[]
            {
                dimension.GroupingLevelKey is { } level ? KeyReference.Level(level, consumer) : null,
                dimension.GroupingAncestorMemberKey is { } ancestor ? KeyReference.Member(ancestor, consumer) : null
            }.OfType<KeyReference>();
        });
    }
}
