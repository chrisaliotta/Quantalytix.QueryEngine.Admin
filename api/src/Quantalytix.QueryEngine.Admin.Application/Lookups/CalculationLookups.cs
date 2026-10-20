using System.Diagnostics;
using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Lookups;

/// <summary>Serves the pickers of calculation models and reports, for <see cref="LookupService"/>.</summary>
public sealed class CalculationLookups(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> reportVersions,
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
                .Select(row => new LookupItem { Id = row.ModelId, Key = row.ModelKey, Label = row.DisplayName })),
            LookupKinds.ModelVersions => [.. (await modelVersions.ListAsync(new { ModelId = parentId }, cancellationToken))
                .OrderBy(row => row.VersionNumber)
                .Select(row => new LookupItem
                {
                    Id = row.ModelVersionId, Key = VersionKey(row.VersionNumber), Label = $"{VersionKey(row.VersionNumber)} · {row.CurrencyCode}", Status = row.Status
                })],
            LookupKinds.Variables => Sorted((await variables.ListAsync(new { ModelVersionId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.VariableId, Key = row.VariableKey, Label = row.DisplayName, Classifier = row.VariableRole })),
            LookupKinds.VariableSources => Sorted((await sources.ListAsync(new { ReportVersionId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.VariableSourceId, Key = row.SourceKey, Label = row.SourceKey, Classifier = row.VariableKey })),
            LookupKinds.SourceProfiles => Sorted((await profiles.ListAsync(new { ReportVersionId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.SourceProfileId, Key = row.ProfileKey, Label = row.DisplayName })),
            LookupKinds.CalculatedMeasures => Sorted((await calculatedMeasures.ListAsync(new { ModelVersionId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.CalculatedMeasureId, Key = row.MeasureKey, Label = row.DisplayName, Classifier = row.DisplayFormat })),
            LookupKinds.CalculationDimensions => Sorted((await calculationDimensions.ListAsync(new { ReportVersionId = parentId }, cancellationToken))
                .Select(row => new LookupItem { Id = row.CalculationDimensionId, Key = row.DimensionKey, Label = row.DisplayName, Classifier = row.ValueKind })),
            LookupKinds.Reports => Sorted((await reports.ListAsync(null, cancellationToken))
                .Select(row => new LookupItem { Id = row.ReportId, Key = row.ReportKey, Label = row.DisplayName, Classifier = row.ReportType })),
            LookupKinds.ReportVersions => [.. (await reportVersions.ListAsync(new { ReportId = parentId }, cancellationToken))
                .OrderBy(row => row.VersionNumber)
                .Select(row => new LookupItem { Id = row.ReportVersionId, Key = VersionKey(row.VersionNumber), Label = VersionKey(row.VersionNumber), Status = row.Status })],
            LookupKinds.ReportColumns => [.. (await columns.ListAsync(new { ReportVersionId = parentId }, cancellationToken))
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

    /// <summary>The key of a version: <c>v</c> followed by its number.</summary>
    private static string VersionKey(int versionNumber)
    {
        return string.Create(CultureInfo.InvariantCulture, $"v{versionNumber}");
    }
}
