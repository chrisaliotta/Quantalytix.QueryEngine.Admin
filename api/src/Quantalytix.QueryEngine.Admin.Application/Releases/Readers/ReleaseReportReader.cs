using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// Reads what reports read from releases and assumption sets: the datasets their sources read, which a release diff names readers
/// of, and the analytical identities they resolve through, which readiness checks a set pins. A report reads the dimensions of the
/// roles its bridge sources and calculation dimensions name, and the dimensions, mapping sets and bridges the bindings of its source
/// datasets' pinned versions resolve through; time bindings resolve through none.
/// </summary>
public sealed class ReleaseReportReader(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportSourceVariableRecord> variables,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    ITableGateway<DimensionBindingRecord> bindings)
{
    /// <summary>The keys of the enabled reports with a source on each dataset, by key.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Report keys by dataset.</returns>
    public async Task<ILookup<long, string>> ReadByAsync(CancellationToken cancellationToken)
    {
        var enabled = (await reports.ListAsync(new { IsEnabled = true }, cancellationToken)).ToDictionary(report => report.ReportId, report => report.ReportKey);
        return (await sources.ListAsync(new { ReportId = enabled.Keys.ToArray() }, cancellationToken))
            .Select(source => (source.DatasetId, ReportKey: enabled[source.ReportId]))
            .Distinct()
            .OrderBy(read => read.ReportKey, StringComparer.Ordinal)
            .ToLookup(read => read.DatasetId, read => read.ReportKey);
    }

    /// <summary>Every report with its sources and the identities it reads under a manifest, by report key.</summary>
    /// <param name="manifest">The manifest whose dataset versions the sources read.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report references.</returns>
    public async Task<IReadOnlyList<ReleaseReportRef>> ListAsync(IReadOnlyList<ReleaseDatasetRecord> manifest, CancellationToken cancellationToken)
    {
        var allSources = await sources.ListAsync(null, cancellationToken);
        var measureKeys = (await variables.ListAsync(new { ReportSourceId = allSources.Select(source => source.ReportSourceId).ToArray() }, cancellationToken))
            .ToLookup(variable => variable.ReportSourceId, variable => variable.MeasureKey);
        var factored = await FactoredBridgesAsync(allSources, cancellationToken);
        var calculations = (await calculationDimensions.ListAsync(null, cancellationToken)).ToLookup(dimension => dimension.ReportId);
        var dimensionOfRole = (await roles.ListAsync(null, cancellationToken)).ToDictionary(role => role.DimensionRoleId, role => role.DimensionId);
        var versionOf = manifest.ToDictionary(row => row.DatasetId, row => row.DatasetVersionId);
        var bindingsOf = (await bindings.ListAsync(new { DatasetVersionId = versionOf.Values.ToArray() }, cancellationToken)).ToLookup(binding => binding.DatasetVersionId);
        var sourcesOf = allSources.ToLookup(source => source.ReportId);
        return
        [
            .. (await reports.ListAsync(null, cancellationToken)).OrderBy(report => report.ReportKey, StringComparer.Ordinal).Select(report => new ReleaseReportRef
            {
                Id = report.ReportId,
                Key = report.ReportKey,
                DisplayName = report.DisplayName,
                IsEnabled = report.IsEnabled,
                Sources =
                [
                    .. sourcesOf[report.ReportId].OrderBy(source => source.SourceKey, StringComparer.Ordinal).Select(source => new ReleaseReportSource
                    {
                        Key = source.SourceKey,
                        DatasetId = source.DatasetId,
                        UsesFactors = source.BridgeId is { } bridgeId && factored.Contains(bridgeId),
                        MeasureKeys = [.. measureKeys[source.ReportSourceId].Distinct().Order(StringComparer.Ordinal)]
                    })
                ],
                Reads =
                [
                    .. sourcesOf[report.ReportId].Select(source => source.DimensionRoleId).OfType<long>()
                        .Concat(calculations[report.ReportId].Select(dimension => dimension.DimensionRoleId))
                        .Select(roleId => Read(PinKinds.Dimension, dimensionOfRole[roleId]))
                        .Concat(sourcesOf[report.ReportId]
                            .Where(source => versionOf.ContainsKey(source.DatasetId))
                            .SelectMany(source => bindingsOf[versionOf[source.DatasetId]])
                            .SelectMany(ReadsOf))
                        .Distinct()
                        .OrderBy(read => PinKinds.Order(read.Kind))
                        .ThenBy(read => read.IdentityId)
                ]
            })
        ];
    }

    /// <summary>The report bridges whose published version adds an item into a row with a factor other than 1.</summary>
    private async Task<IReadOnlySet<long>> FactoredBridgesAsync(IReadOnlyList<ReportSourceRecord> allSources, CancellationToken cancellationToken)
    {
        long[] bridgeIds = [.. allSources.Select(source => source.BridgeId).OfType<long>().Distinct()];
        var published = (await bridgeVersions.ListAsync(new { BridgeId = bridgeIds, Status = PublicationStatuses.Published }, cancellationToken))
            .ToDictionary(version => version.BridgeVersionId, version => version.BridgeId);
        return (await bridgeMappings.ListAsync(new { BridgeVersionId = published.Keys.ToArray() }, cancellationToken))
            .Where(mapping => mapping.Factor != 1m)
            .Select(mapping => published[mapping.BridgeVersionId])
            .ToHashSet();
    }

    /// <summary>The identities a binding resolves through: its dimension, and its mapping set or bridge; none for a time binding.</summary>
    private static IEnumerable<ReleaseReportRead> ReadsOf(DimensionBindingRecord binding)
    {
        if (binding.BindingKind == BindingKinds.Time) yield break;
        yield return Read(PinKinds.Dimension, binding.DimensionId);
        if (binding.MappingSetId is { } mappingSetId) yield return Read(PinKinds.MappingSet, mappingSetId);
        if (binding.BridgeId is { } bridgeId) yield return Read(PinKinds.Bridge, bridgeId);
    }

    /// <summary>A read of one identity.</summary>
    private static ReleaseReportRead Read(string kind, long identityId)
    {
        return new ReleaseReportRead { Kind = kind, IdentityId = identityId };
    }
}
