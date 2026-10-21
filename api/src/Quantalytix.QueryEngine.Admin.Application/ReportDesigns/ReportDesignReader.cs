using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Reads a report version as one design: the members of its report dimension's paired version and its overlay rows, the items
/// each row's sources map to it through the paired bridge versions, and the variable sources grouped into design sources by bridge.
/// A member row is a data row when its member is assignable, so it takes items and the rows under it add to its total, and a group
/// otherwise.
/// </summary>
public sealed class ReportDesignReader(
    ReportDesignVersions designs,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<ReportMemberOptionRecord> options,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<DimensionMappingRecord> mappingEntries)
{
    /// <summary>Reads a report version's design.</summary>
    /// <param name="reportVersionId">A draft, validated or published report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The design.</returns>
    public async Task<ReportDesign> ReadAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        var scope = await designs.ReadAsync(reportVersionId, cancellationToken);
        var versionSources = await sources.ListAsync(new { ReportVersionId = reportVersionId }, cancellationToken);
        var designSources = DesignSources(scope, versionSources);
        var used = designSources.Select(source => source.BridgeId).ToHashSet();
        var bridgeOf = scope.BridgeVersions.Values
            .Where(bridgeVersion => used.Contains(bridgeVersion.BridgeId))
            .ToDictionary(bridgeVersion => bridgeVersion.BridgeVersionId, bridgeVersion => bridgeVersion.BridgeId);
        var mappings = (await bridgeMappings.ListAsync(new { BridgeVersionId = bridgeOf.Keys.ToArray() }, cancellationToken))
            .OrderBy(mapping => bridgeOf[mapping.BridgeVersionId])
            .ThenBy(mapping => mapping.SourceMemberKey, StringComparer.Ordinal)
            .ToLookup(
                mapping => mapping.TargetMemberKey,
                mapping => new DesignRowMapping { BridgeId = bridgeOf[mapping.BridgeVersionId], ItemKey = mapping.SourceMemberKey, Factor = mapping.Factor },
                StringComparer.Ordinal);
        var codes = await MappedCodesAsync(scope.Report.ReportDimensionId, cancellationToken);
        IEnumerable<DesignRow> rows = [.. await MemberRowsAsync(scope, mappings, codes, cancellationToken), .. await OverlayRowsAsync(reportVersionId, cancellationToken)];
        return new ReportDesign
        {
            ReportId = scope.Report.ReportId,
            ReportVersionId = reportVersionId,
            Status = scope.Version.Status,
            DimensionVersionId = scope.Structure.DimensionVersionId,
            Rows = [.. rows.OrderBy(row => row.SortOrder).ThenBy(row => row.Key, StringComparer.Ordinal)],
            Sources = designSources,
            OtherSources =
            [
                .. versionSources
                    .Where(source => source.ClassificationKind != ClassificationKinds.Bridge)
                    .OrderBy(source => source.SourceKey, StringComparer.OrdinalIgnoreCase)
            ]
        };
    }

    /// <summary>The members of the paired report dimension version as rows, with the member options that hide them or make them headers.</summary>
    private async Task<IEnumerable<DesignRow>> MemberRowsAsync(
        ReportDesignScope scope,
        ILookup<string, DesignRowMapping> mappings,
        ILookup<string, DesignMappedCode> codes,
        CancellationToken cancellationToken)
    {
        var tree = await members.ListAsync(new { scope.Structure.DimensionVersionId }, cancellationToken);
        var keyOf = tree.ToDictionary(member => member.MemberId, member => member.MemberKey);
        var optionOf = (await options.ListAsync(new { scope.Version.ReportVersionId }, cancellationToken))
            .ToDictionary(option => option.MemberKey, StringComparer.Ordinal);
        return tree.Select(member =>
        {
            var option = optionOf.GetValueOrDefault(member.MemberKey);
            return new DesignRow
            {
                Key = member.MemberKey,
                Label = option?.DisplayName ?? member.DisplayName,
                Kind = member.IsAssignable ? DesignRowKinds.Data : DesignRowKinds.Group,
                ParentKey = member.ParentMemberId is { } parentId ? keyOf[parentId] : null,
                SortOrder = member.SortOrder,
                IsVisible = option?.IsVisible ?? true,
                ShowsValues = option?.ShowsValues ?? true,
                Mappings = [.. mappings[member.MemberKey]],
                MappedCodes = [.. codes[member.MemberKey]]
            };
        });
    }

    /// <summary>The report version's overlay rows, with the key of the column a formula row fills.</summary>
    private async Task<IEnumerable<DesignRow>> OverlayRowsAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        var columnKeys = (await columns.ListAsync(new { ReportVersionId = reportVersionId }, cancellationToken))
            .ToDictionary(column => column.ReportColumnId, column => column.ColumnKey);
        return (await lines.ListAsync(new { ReportVersionId = reportVersionId }, cancellationToken)).Select(line => new DesignRow
        {
            Key = line.LineKey,
            Label = line.DisplayName,
            Kind = line.LineKind,
            ParentKey = line.ParentKey,
            SortOrder = line.SortOrder,
            IsVisible = line.IsVisible,
            ShowsValues = true,
            CalculatedMeasureKey = line.CalculatedMeasureKey,
            ValueColumnKey = line.ValueColumnId is { } columnId ? columnKeys[columnId] : null,
            Mappings = [],
            MappedCodes = []
        });
    }

    /// <summary>The source codes the published versions of the mapping sets into the report dimension map, by member key.</summary>
    private async Task<ILookup<string, DesignMappedCode>> MappedCodesAsync(long reportDimensionId, CancellationToken cancellationToken)
    {
        var setIds = (await mappingSets.ListAsync(new { DimensionId = reportDimensionId }, cancellationToken)).Select(set => set.MappingSetId).ToArray();
        var setOf = (await mappingVersions.ListAsync(new { MappingSetId = setIds, Status = PublicationStatuses.Published }, cancellationToken))
            .ToDictionary(version => version.MappingVersionId, version => version.MappingSetId);
        var entries = await mappingEntries.ListAsync(new { MappingVersionId = setOf.Keys.ToArray() }, cancellationToken);
        return entries
            .OrderBy(entry => entry.SourceKeyValue, StringComparer.Ordinal)
            .ToLookup(
                entry => entry.MemberKey,
                entry => new DesignMappedCode { MappingSetId = setOf[entry.MappingVersionId], SourceKey = entry.SourceKeyValue },
                StringComparer.Ordinal);
    }

    /// <summary>
    /// The version's bridge-classified variable sources, one design source per bridge. Every bridge classification leads into the
    /// report dimension, so each bridge is the report's own.
    /// </summary>
    private static IReadOnlyList<DesignSource> DesignSources(ReportDesignScope scope, IReadOnlyList<VariableSourceRecord> versionSources)
    {
        return
        [
            .. versionSources
                .Where(source => source.ClassificationKind == ClassificationKinds.Bridge)
                .GroupBy(source => source.BridgeId!.Value)
                .Select(group =>
                {
                    var bridge = scope.Bridges[group.Key];
                    var first = group.First();
                    var uniform = DesignSourceShape.IsUniform([.. group]);
                    return new DesignSource
                    {
                        BridgeId = bridge.BridgeId,
                        Key = DesignSourceKeys.Source(scope.Report.ReportKey, bridge.BridgeKey),
                        Label = bridge.DisplayName,
                        SourceDimensionId = bridge.SourceDimensionId,
                        IsUniform = uniform,
                        RoleId = uniform ? first.ClassificationRoleId : null,
                        DatasetId = uniform ? first.DatasetId : null,
                        TimeFieldKey = uniform ? first.TimeFieldKey : null,
                        SourceFilterJson = uniform ? first.SourceFilterJson : null,
                        Variables = [.. group.OrderBy(source => source.VariableKey, StringComparer.Ordinal)]
                    };
                })
                .OrderBy(source => source.Key, StringComparer.OrdinalIgnoreCase)
        ];
    }
}
