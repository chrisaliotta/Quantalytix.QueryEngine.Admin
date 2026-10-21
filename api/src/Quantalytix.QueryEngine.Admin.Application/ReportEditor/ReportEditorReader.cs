using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Reads a report as its editor shows it: the members of its report dimension and its overlay rows, the rules each bridge source's
/// bridge adds into each row, and the sources with their variables. A member row is a data row when its member is assignable, so it
/// takes items and the rows under it add to its total, and a group otherwise.
/// </summary>
public sealed class ReportEditorReader(
    ITableGateway<ReportRecord> reports,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<ReportMemberOptionRecord> options,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DimensionRoleRecord> roles,
    OwnedVersions owned,
    PublishedDimensionReader publishedDimensions)
{
    /// <summary>Reads a report's editor view.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The editor view.</returns>
    public async Task<EditorView> ReadAsync(long reportId, CancellationToken cancellationToken)
    {
        var owner = new { ReportId = reportId };
        var report = await reports.GetAsync(owner, cancellationToken);
        var structure = await owned.StructureAsync(report, cancellationToken);
        var reportSources = await sources.ListAsync(owner, cancellationToken);
        var bridgeVersions = await owned.BridgesAsync(reportSources, cancellationToken);
        var sourceOf = bridgeVersions.ToDictionary(pair => pair.Value.BridgeVersionId, pair => pair.Key);
        var entries = await bridgeMappings.ListAsync(new { BridgeVersionId = sourceOf.Keys.ToArray() }, cancellationToken);
        var items = entries
            .OrderBy(entry => sourceOf[entry.BridgeVersionId])
            .ThenBy(entry => entry.SourceMemberKey, StringComparer.Ordinal)
            .ToLookup(
                entry => entry.TargetMemberKey,
                entry => new EditorRowItem
                {
                    ReportSourceId = sourceOf[entry.BridgeVersionId],
                    ItemKey = entry.SourceMemberKey,
                    Selection = entry.SourceSelection,
                    Factor = entry.Factor
                },
                StringComparer.Ordinal);
        IEnumerable<EditorRow> rows =
        [
            .. await MemberRowsAsync(reportId, structure.DimensionVersionId, items, cancellationToken),
            .. await OverlayRowsAsync(reportId, cancellationToken)
        ];
        return new EditorView
        {
            Report = report,
            DimensionVersionId = structure.DimensionVersionId,
            Rows = [.. rows.OrderBy(row => row.SortOrder).ThenBy(row => row.Key, StringComparer.Ordinal)],
            Sources = await SourcesAsync(reportSources, bridgeVersions, entries.ToLookup(entry => entry.BridgeVersionId), cancellationToken)
        };
    }

    /// <summary>The members of the report dimension's version as rows, with the member options that hide them or make them headers.</summary>
    private async Task<IEnumerable<EditorRow>> MemberRowsAsync(
        long reportId,
        long structureId,
        ILookup<string, EditorRowItem> items,
        CancellationToken cancellationToken)
    {
        var tree = await members.ListAsync(new { DimensionVersionId = structureId }, cancellationToken);
        var keyOf = tree.ToDictionary(member => member.MemberId, member => member.MemberKey);
        var optionOf = (await options.ListAsync(new { ReportId = reportId }, cancellationToken)).ToDictionary(option => option.MemberKey, StringComparer.Ordinal);
        return tree.Select(member =>
        {
            var option = optionOf.GetValueOrDefault(member.MemberKey);
            return new EditorRow
            {
                Key = member.MemberKey,
                Label = option?.DisplayName ?? member.DisplayName,
                Kind = member.IsAssignable ? ReportRowKinds.Data : ReportRowKinds.Group,
                ParentKey = member.ParentMemberId is { } parentId ? keyOf[parentId] : null,
                SortOrder = member.SortOrder,
                IsVisible = option?.IsVisible ?? true,
                ShowsValues = option?.ShowsValues ?? true,
                Items = [.. items[member.MemberKey]]
            };
        });
    }

    /// <summary>The report's overlay rows, with the key of the column a formula row fills.</summary>
    private async Task<IEnumerable<EditorRow>> OverlayRowsAsync(long reportId, CancellationToken cancellationToken)
    {
        var owner = new { ReportId = reportId };
        var columnKeys = (await columns.ListAsync(owner, cancellationToken)).ToDictionary(column => column.ReportColumnId, column => column.ColumnKey);
        return (await lines.ListAsync(owner, cancellationToken)).Select(line => new EditorRow
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
            Items = []
        });
    }

    /// <summary>The report's sources with their datasets, item dimensions, bridge versions, unmapped counts and variables, by key.</summary>
    private async Task<IReadOnlyList<EditorSource>> SourcesAsync(
        IReadOnlyList<ReportSourceRecord> reportSources,
        IReadOnlyDictionary<long, BridgeVersionRecord> bridgeVersions,
        ILookup<long, BridgeMappingRecord> entriesOf,
        CancellationToken cancellationToken)
    {
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = reportSources.Select(source => source.DatasetId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var rolesById = (await roles.ListAsync(new { RoleId = reportSources.Select(source => source.RoleId).OfType<long>().Distinct().ToArray() }, cancellationToken))
            .ToDictionary(role => role.RoleId);
        var variablesOf = (await sourceVariables.ListAsync(new { ReportSourceId = reportSources.Select(source => source.ReportSourceId).ToArray() }, cancellationToken))
            .ToLookup(variable => variable.ReportSourceId);
        var editorSources = new List<EditorSource>();
        foreach (var source in reportSources.OrderBy(source => source.SourceKey, StringComparer.OrdinalIgnoreCase))
        {
            var role = source.RoleId is { } roleId ? rolesById[roleId] : null;
            var itemKeys = role is null ? null : await publishedDimensions.ReadAsync(role.DimensionId, cancellationToken);
            var bridgeVersion = bridgeVersions.GetValueOrDefault(source.ReportSourceId);
            editorSources.Add(new EditorSource
            {
                Source = source,
                DatasetKey = datasetKeys[source.DatasetId],
                RoleKey = role?.RoleKey,
                ItemDimensionId = role?.DimensionId,
                ItemDimensionVersionId = itemKeys?.Version.DimensionVersionId,
                BridgeVersionId = bridgeVersion?.BridgeVersionId,
                UnmappedCount = itemKeys is null || bridgeVersion is null ? null : SourceCoverageReader.Unmapped(itemKeys, entriesOf[bridgeVersion.BridgeVersionId]).Count,
                Variables = [.. variablesOf[source.ReportSourceId].OrderBy(variable => variable.VariableKey, StringComparer.Ordinal)]
            });
        }
        return editorSources;
    }
}
