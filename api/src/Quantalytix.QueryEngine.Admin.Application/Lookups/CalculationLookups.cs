using System.Diagnostics;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Lookups;

/// <summary>Serves the pickers of calculation models and reports, for <see cref="LookupService"/>.</summary>
public sealed class CalculationLookups(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportColumnRecord> columns)
{
    /// <summary>Lists one calculation or report picker, sorted by key unless the kind has a natural order.</summary>
    /// <param name="kind">A calculation or report <c>lookup_kind</c> value.</param>
    /// <param name="parentId">The owning row's ID; present for child kinds.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The picker's items.</returns>
    public async Task<IReadOnlyList<LookupItem>> ListAsync(string kind, long? parentId, CancellationToken cancellationToken)
    {
        return kind switch
        {
            LookupKinds.CalculationModels => Sorted((await models.ListAsync(null, cancellationToken))
                .Select(row => new LookupItem { Id = row.CalculationModelId, Key = row.ModelKey, Label = row.DisplayName, Classifier = row.CurrencyCode })),
            LookupKinds.Variables => Sorted((await variables.ListAsync(new { CalculationModelId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.VariableId, Key = row.VariableKey, Label = row.DisplayName, Classifier = row.DisplayFormat })),
            LookupKinds.ReportSources => Sorted((await sources.ListAsync(new { ReportId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.ReportSourceId, Key = row.SourceKey, Label = row.DisplayName, Classifier = row.MappingMethod })),
            LookupKinds.SourceProfiles => Sorted((await profiles.ListAsync(new { ReportId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.CalculationSourceProfileId, Key = row.ProfileKey, Label = row.DisplayName })),
            LookupKinds.CalculatedMeasures => Sorted((await calculatedMeasures.ListAsync(new { CalculationModelId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.CalculatedMeasureId, Key = row.MeasureKey, Label = row.DisplayName, Classifier = row.DisplayFormat })),
            LookupKinds.CalculationDimensions => Sorted((await calculationDimensions.ListAsync(new { ReportId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.CalculationDimensionId, Key = row.DimensionKey, Label = row.DisplayName, Classifier = row.GroupingMode })),
            LookupKinds.Reports => Sorted((await reports.ListAsync(null, cancellationToken))
                .Select(row => new LookupItem { Id = row.ReportId, Key = row.ReportKey, Label = row.DisplayName, Classifier = row.ReportType })),
            LookupKinds.ReportColumns => [.. (await columns.ListAsync(new { ReportId = parentId }, cancellationToken))
                .OrderBy(row => row.SortOrder)
                .Select(row => new LookupItem { Id = row.ReportColumnId, Key = row.ColumnKey, Label = row.DisplayName ?? row.MetricKey, Classifier = row.ValueKind })],
            _ => throw new UnreachableException($"Lookup kind '{kind}' passed validation but has no list.")
        };
    }

    /// <summary>Orders items by key, ignoring case.</summary>
    private static IReadOnlyList<LookupItem> Sorted(IEnumerable<LookupItem> items)
    {
        return [.. items.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)];
    }
}
