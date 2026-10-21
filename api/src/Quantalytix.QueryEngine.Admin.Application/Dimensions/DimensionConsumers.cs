using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Lists the level and member keys that live objects name in a dimension: the unknown members of bindings of published dataset
/// versions, entries of published mapping versions, mappings of published bridge versions, and the member options, overlay row
/// parents, variable scopes and the groupings of calculation dimensions on the dimension's roles of published report versions.
/// They resolve the keys in the dimension's published version, so publishing another version or deleting from the published one
/// must keep the keys. While a set of versions validates together, a mapping, bridge or report version of the set counts as its
/// identity's published version.
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
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportLineRecord> reportLines,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    BindingNames names,
    PublicationSet publicationSet)
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
        var published = publicationSet.CountingAsPublished(
            VersionKinds.MappingVersion,
            await mappingVersions.ListAsync(new { MappingSetId = setKeys.Keys.ToArray() }, cancellationToken),
            version => version.MappingSetId,
            version => version.MappingVersionId,
            version => version.Status);
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
        var published = publicationSet.CountingAsPublished(
            VersionKinds.BridgeVersion,
            await bridgeVersions.ListAsync(new { BridgeId = endpoints.Keys.ToArray() }, cancellationToken),
            version => version.BridgeId,
            version => version.BridgeVersionId,
            version => version.Status);
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

    /// <summary>
    /// The member keys the published versions of reports on the dimension name: member options, the parents of overlay rows that are
    /// no overlay row, and variable scopes; and the grouping keys of published report versions' calculation dimensions on its roles.
    /// </summary>
    private async Task<IEnumerable<KeyReference>> ReportsAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var published = publicationSet.CountingAsPublished(
            VersionKinds.ReportVersion,
            await reportVersions.ListAsync(null, cancellationToken),
            version => version.ReportId,
            version => version.ReportVersionId,
            version => version.Status);
        var reportsById = (await reports.ListAsync(new { ReportId = published.Select(version => version.ReportId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId);
        var reportOf = published.ToDictionary(version => version.ReportVersionId, version => reportsById[version.ReportId]);
        long[] onDimension = [.. reportOf.Where(pair => pair.Value.ReportDimensionId == dimensionId).Select(pair => pair.Key)];
        var options = await memberOptions.ListAsync(new { ReportVersionId = onDimension }, cancellationToken);
        var lines = await reportLines.ListAsync(new { ReportVersionId = onDimension }, cancellationToken);
        var overlayKeys = lines.Select(line => (line.ReportVersionId, line.LineKey)).ToHashSet();
        var scoping = await scopes.ListAsync(new { ReportVersionId = onDimension }, cancellationToken);
        var roleIds = (await roles.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).Select(role => role.RoleId).ToArray();
        var grouping = await calculationDimensions.ListAsync(new { ReportVersionId = reportOf.Keys.ToArray(), RoleId = roleIds }, cancellationToken);
        return
        [
            .. options.Select(option => KeyReference.Member(option.MemberKey, $"Member option of report '{reportOf[option.ReportVersionId].ReportKey}'")),
            .. lines
                .Where(line => line.ParentKey is { } parent && !overlayKeys.Contains((line.ReportVersionId, parent)))
                .Select(line => KeyReference.Member(line.ParentKey!, $"Row '{line.LineKey}' of report '{reportOf[line.ReportVersionId].ReportKey}'")),
            .. scoping.Select(scope =>
                KeyReference.Member(scope.ScopeMemberKey, $"Scope of variable '{scope.VariableKey}' of report '{reportOf[scope.ReportVersionId].ReportKey}'")),
            .. grouping.SelectMany(dimension =>
            {
                var consumer = $"Calculation dimension '{dimension.DimensionKey}' of report '{reportOf[dimension.ReportVersionId].ReportKey}'";
                return new[]
                {
                    dimension.GroupingLevelKey is { } level ? KeyReference.Level(level, consumer) : null,
                    dimension.GroupingAncestorMemberKey is { } ancestor ? KeyReference.Member(ancestor, consumer) : null
                }.OfType<KeyReference>();
            })
        ];
    }
}
