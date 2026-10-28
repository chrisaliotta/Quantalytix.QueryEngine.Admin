using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The report lifecycle commands that create and enable reports, which run on stored state, never inside a change set: create a
/// disabled report with its report dimension (P1, P3), and enable or disable it (P2). <see cref="ReportRemover"/> deletes reports.
/// </summary>
/// <param name="reports">The report table.</param>
/// <param name="dimensions">Reads the taken dimension keys.</param>
/// <param name="reportDimensions">Creates the report dimension.</param>
/// <param name="publishedDimensions">Reads the start-from dimension's published version.</param>
/// <param name="workflow">Publishes the report dimension's version 1.</param>
/// <param name="models">Reads the model a copy resolves in.</param>
/// <param name="loader">Reads a report to enable.</param>
/// <param name="rules">Runs the report rules.</param>
/// <param name="queries">Reads the report's summary and the copied report.</param>
/// <param name="changes">Writes a copy's content.</param>
/// <param name="semanticRevision">Advances when the engine starts or stops reading a report.</param>
/// <param name="ids">Allocates the report's id.</param>
public sealed class ReportCommands(
    ITableGateway<ReportRecord> reports,
    ITableGateway<DimensionRecord> dimensions,
    ReportDimensions reportDimensions,
    PublishedDimensionReader publishedDimensions,
    VersionWorkflow workflow,
    ModelSnapshotLoader models,
    ReportSnapshotLoader loader,
    ReportRules rules,
    ReportQueries queries,
    ReportChanges changes,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>
    /// Creates a disabled report and first its report dimension, keyed and named like the report, whose published version 1 copies
    /// the start-from dimension's published version, the copied report's dimension, or holds a single root row. A copy then takes the
    /// copied report's content through a change set (<see cref="ReportCopy"/>).
    /// </summary>
    /// <param name="input">The new report, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The report's summary, or a conflict when its key is taken.</returns>
    public async Task<Outcome<ReportSummary>> CreateAsync(ReportCreateInput input, CancellationToken cancellationToken)
    {
        if ((await reports.ListAsync(new { ReportKey = input.Key }, cancellationToken)).Count > 0)
            return Outcome<ReportSummary>.Refused(Refusal.Conflict("Key taken", $"A report with key '{input.Key}' already exists."));
        if ((await dimensions.ListAsync(new { DimensionKey = input.Key }, cancellationToken)).Count > 0)
            return Outcome<ReportSummary>.Refused(Refusal.Conflict("Key taken", $"The report's own dimension takes its key, and a dimension with key '{input.Key}' already exists."));

        var copied = input.CopyFromReportId is { } copyFrom ? await queries.DocumentAsync(copyFrom, cancellationToken) : null;
        long? templateVersionId = copied is not null
            ? copied.ReportDimensionVersionId
            : input.StartFromDimensionId is { } templateId ? (await publishedDimensions.ReadAsync(templateId, cancellationToken))!.Version.DimensionVersionId : null;
        var structure = await reportDimensions.CreateAsync(input.Key, input.DisplayName, templateVersionId, cancellationToken);
        await workflow.PublishAsync(VersionKinds.DimensionVersion, structure.DimensionVersionId, cancellationToken);
        var report = new ReportRecord
        {
            ReportId = ids.NextId(),
            ReportKey = input.Key,
            DisplayName = input.DisplayName,
            ReportType = input.ReportType,
            ReportDimensionId = structure.DimensionId,
            CalculationModelId = input.ModelId,
            SubtotalPosition = SubtotalPositions.Above,
            IsEnabled = false,
            DefaultSettingsJson = ReportSettingsColumns.DefaultSettingsJson
        };
        await reports.InsertAsync(report, cancellationToken);

        if (copied is not null)
        {
            var operations = ReportCopy.Operations(copied, await models.LoadGraphAsync(input.ModelId, cancellationToken));
            var commit = await changes.CommitAsync(report.ReportId, new ChangeSetRequest<ReportOperation> { Operations = operations }, cancellationToken);
            if (!commit.Succeeded) return Outcome<ReportSummary>.Refused(commit.Refusal);
        }

        return Outcome<ReportSummary>.Success(await queries.SummaryAsync(report.ReportId, cancellationToken));
    }

    /// <summary>Enables a report that passes every report rule, or disables it; a change advances the semantic revision.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">Whether it is enabled.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The report's summary, or the rules it breaks, in which case it stays disabled.</returns>
    public async Task<Outcome<ReportSummary>> SetEnabledAsync(long reportId, ReportEnabledInput input, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadAsync(reportId, cancellationToken);
        var report = snapshot.Content.Report;
        if (input.IsEnabled && !report.IsEnabled && await rules.EvaluateAsync([snapshot.Content], cancellationToken) is { Count: > 0 } violations)
            return Outcome<ReportSummary>.Refused(Refusal.RulesViolated("The report breaks report rules, so it stays disabled.", violations));
        if (input.IsEnabled != report.IsEnabled)
        {
            await reports.UpdateAsync(report with { IsEnabled = input.IsEnabled }, cancellationToken);
            await semanticRevision.AdvanceAsync(cancellationToken);
        }

        return Outcome<ReportSummary>.Success(await queries.SummaryAsync(reportId, cancellationToken));
    }
}
