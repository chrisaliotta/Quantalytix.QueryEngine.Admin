using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Lists the level and member keys that live objects name in a dimension: bindings of published dataset versions, entries of
/// published mapping versions, mappings of published bridge versions, lines of published report versions, and the variable
/// targets of the published versions of those reports' models. They resolve the keys in the dimension's published version,
/// so publishing another version or deleting from the published one must keep the keys.
/// </summary>
public sealed class DimensionConsumers(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<DimensionMappingRecord> mappingEntries,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<ReportLineRecord> reportLines,
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ITableGateway<VariableRecord> variables)
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

    /// <summary>The grouping level, grouping ancestor and unknown member keys of the bindings of published dataset versions.</summary>
    private async Task<IEnumerable<KeyReference>> BindingsAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var published = await datasetVersions.ListAsync(new { Status = PublicationStatuses.Published }, cancellationToken);
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = published.Select(version => version.DatasetId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var datasetOf = published.ToDictionary(version => version.DatasetVersionId, version => datasetKeys[version.DatasetId]);
        var bound = await bindings.ListAsync(new { DimensionId = dimensionId, DatasetVersionId = datasetOf.Keys.ToArray() }, cancellationToken);
        return bound.SelectMany(binding =>
        {
            var consumer = $"Binding '{binding.DisplayName}' of dataset '{datasetOf[binding.DatasetVersionId]}'";
            return new[]
            {
                binding.GroupingLevelKey is { } level ? KeyReference.Level(level, consumer) : null,
                binding.GroupingAncestorMemberKey is { } ancestor ? KeyReference.Member(ancestor, consumer) : null,
                binding.UnknownMemberKey is { } unknown ? KeyReference.Member(unknown, consumer) : null
            }.OfType<KeyReference>();
        });
    }

    /// <summary>The member keys of the entries of the published versions of the dimension's mapping sets.</summary>
    private async Task<IEnumerable<KeyReference>> MappingEntriesAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var setKeys = (await mappingSets.ListAsync(new { DimensionId = dimensionId }, cancellationToken))
            .ToDictionary(set => set.MappingSetId, set => set.MappingSetKey);
        var published = await mappingVersions.ListAsync(new { MappingSetId = setKeys.Keys.ToArray(), Status = PublicationStatuses.Published }, cancellationToken);
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
        var published = await bridgeVersions.ListAsync(new { BridgeId = endpoints.Keys.ToArray(), Status = PublicationStatuses.Published }, cancellationToken);
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
    /// The member keys of the lines of the published report versions on the dimension, and of the variable targets of the
    /// published versions of their models.
    /// </summary>
    private async Task<IEnumerable<KeyReference>> ReportsAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var published = await reportVersions.ListAsync(new { ReportDimensionId = dimensionId, Status = PublicationStatuses.Published }, cancellationToken);
        var reportKeys = (await reports.ListAsync(new { ReportId = published.Select(version => version.ReportId).ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId, report => report.ReportKey);
        var reportOf = published.ToDictionary(version => version.ReportVersionId, version => reportKeys[version.ReportId]);
        var lines = await reportLines.ListAsync(new { ReportVersionId = reportOf.Keys.ToArray() }, cancellationToken);

        long[] modelIds = [.. published.Select(version => version.ModelId).Distinct()];
        var modelKeys = (await models.ListAsync(new { ModelId = modelIds }, cancellationToken)).ToDictionary(model => model.ModelId, model => model.ModelKey);
        var modelOf = (await modelVersions.ListAsync(new { ModelId = modelIds, Status = PublicationStatuses.Published }, cancellationToken))
            .ToDictionary(version => version.ModelVersionId, version => modelKeys[version.ModelId]);
        var targeting = await variables.ListAsync(new { ModelVersionId = modelOf.Keys.ToArray() }, cancellationToken);
        return
        [
            .. lines
                .Where(line => line.ReportMemberKey is not null)
                .Select(line => KeyReference.Member(line.ReportMemberKey!, $"Line '{line.LineKey}' of report '{reportOf[line.ReportVersionId]}'")),
            .. targeting
                .Where(variable => variable.TargetMemberKey is not null)
                .Select(variable => KeyReference.Member(variable.TargetMemberKey!, $"Variable '{variable.VariableKey}' of model '{modelOf[variable.ModelVersionId]}'"))
        ];
    }
}
