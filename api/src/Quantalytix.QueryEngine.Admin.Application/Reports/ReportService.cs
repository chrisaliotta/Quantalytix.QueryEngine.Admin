using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads, creates, edits and deletes reports. Each report reads one calculation model, fixed at creation, and owns its report
/// dimension, whose one version is published from the start and edited in place, and the bridges of its sources. A report runs on
/// the active assumption set and release once it is enabled, which <see cref="ReportStatusService"/> does.
/// </summary>
public sealed class ReportService(
    ITableGateway<ReportRecord> reports,
    DimensionService dimensionService,
    PublishedDimensionReader publishedDimensions,
    VersionWorkflow workflow,
    OwnedVersions owned,
    ReportContentRemover remover,
    AssumptionPins pins,
    ReportChanges changes,
    IIdGenerator ids)
{
    /// <summary>The settings a new report starts with: monthly periods in UTC.</summary>
    public const string DefaultSettingsJson = """{"periodicity":"month","timeZone":"UTC"}""";

    /// <summary>Lists every report.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The reports by key, ignoring case.</returns>
    public async Task<IReadOnlyList<ReportRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await reports.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.ReportKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one report.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The report.</returns>
    public Task<ReportRecord> GetAsync(long reportId, CancellationToken cancellationToken)
    {
        return reports.GetAsync(new { ReportId = reportId }, cancellationToken);
    }

    /// <summary>
    /// Creates a disabled report, and first its own report dimension, keyed and named like the report, whose version 1 copies the
    /// published version of the dimension the report starts from, or holds a single root row, and is published at once, which pins
    /// it in the active assumption set.
    /// </summary>
    /// <param name="input">The new report, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored report.</returns>
    public async Task<ReportRecord> CreateAsync(NewReportInput input, CancellationToken cancellationToken)
    {
        long? templateVersionId = input.StartFromDimensionId is { } templateId
            ? (await publishedDimensions.ReadAsync(templateId, cancellationToken))!.Version.DimensionVersionId
            : null;
        var structure = await dimensionService.CreateReportDimensionAsync(input.ReportKey, input.DisplayName, templateVersionId, cancellationToken);
        await workflow.PublishAsync(VersionKinds.DimensionVersion, structure.DimensionVersionId, cancellationToken);
        var report = new ReportRecord
        {
            ReportId = ids.NextId(),
            ReportKey = input.ReportKey,
            DisplayName = input.DisplayName,
            ReportType = input.ReportType,
            ReportDimensionId = structure.DimensionId,
            ModelId = input.ModelId,
            SubtotalPosition = SubtotalPositions.Above,
            IsEnabled = false,
            PayloadJson = input.PayloadJson,
            DefaultSettingsJson = DefaultSettingsJson
        };
        await reports.InsertAsync(report, cancellationToken);
        return report;
    }

    /// <summary>Edits a report's identity attributes, which no report rule reads.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored report.</returns>
    public async Task<ReportRecord> UpdateAsync(long reportId, ReportInput input, CancellationToken cancellationToken)
    {
        var updated = await GetAsync(reportId, cancellationToken) with { DisplayName = input.DisplayName, ReportType = input.ReportType, PayloadJson = input.PayloadJson };
        await reports.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Replaces a report's settings: its default profile, subtotal position and defaults.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The new settings, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The violations that kept an enabled report from taking the settings, in which case nothing was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> UpdateSettingsAsync(long reportId, ReportSettingsInput input, CancellationToken cancellationToken)
    {
        var updated = await GetAsync(reportId, cancellationToken) with
        {
            DefaultProfileKey = input.DefaultProfileKey,
            SubtotalPosition = input.SubtotalPosition,
            DefaultFilterJson = input.DefaultFilterJson,
            DefaultSettingsJson = input.DefaultSettingsJson,
            DefaultPivotJson = input.DefaultPivotJson
        };
        return await changes.ApplyAsync(reportId, report => report with { Report = updated }, () => reports.UpdateAsync(updated, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Deletes a report with everything it owns: its content, its sources with their bridges, then the report, and its report
    /// dimension, unpinned first. Nothing outside the report names what it owns, so nothing blocks the delete.
    /// </summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the report is gone.</returns>
    public async Task DeleteAsync(long reportId, CancellationToken cancellationToken)
    {
        var report = await GetAsync(reportId, cancellationToken);
        var structure = await owned.StructureAsync(report, cancellationToken);
        await remover.RemoveContentAsync(reportId, cancellationToken);
        await reports.DeleteAsync(new { ReportId = reportId }, cancellationToken);
        await pins.UnpinDimensionAsync(report.ReportDimensionId, cancellationToken);
        await dimensionService.RemoveAsync(report.ReportDimensionId, [structure], cancellationToken);
    }
}
