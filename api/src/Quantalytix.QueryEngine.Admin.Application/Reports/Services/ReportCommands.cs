using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The report commands that create and enable reports, which run on stored state, never inside a change set: create a report with a
/// disabled live version and its report dimension (P1, P3), and enable or disable the live version (P2). Both advance the semantic
/// revision. <see cref="ReportRemover"/> deletes reports.
/// </summary>
/// <param name="reports">The report identities.</param>
/// <param name="versions">The report versions.</param>
/// <param name="dimensions">Reads the taken dimension keys.</param>
/// <param name="reportDimensions">Creates the report dimension.</param>
/// <param name="dimensionKeys">Reads the start-from dimension's live version.</param>
/// <param name="models">Reads the model a copy resolves in.</param>
/// <param name="loader">Reads a report to enable.</param>
/// <param name="rules">Runs the report rules.</param>
/// <param name="queries">Reads the report's summary and the copied report.</param>
/// <param name="changes">Writes a copy's content.</param>
/// <param name="live">The live snapshot.</param>
/// <param name="semanticRevision">The tenant's semantic revision.</param>
/// <param name="ids">Allocates the report's ids.</param>
/// <param name="time">Stamps the new version.</param>
public sealed class ReportCommands(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<DimensionRecord> dimensions,
    ReportDimensions reportDimensions,
    SnapshotDimensionReader dimensionKeys,
    ModelSnapshotLoader models,
    ReportSnapshotLoader loader,
    ReportRules rules,
    ReportQueries queries,
    ReportChanges changes,
    LiveSnapshot live,
    SemanticRevision semanticRevision,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>
    /// Creates a report with a disabled live version, and first its report dimension, keyed and named like the report, whose live
    /// version copies the start-from dimension's live version, the copied report's dimension, or holds a single root row. A copy then
    /// takes the copied report's content through a change set (<see cref="ReportCopy"/>).
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

        var liveSnapshotId = await live.IdAsync(cancellationToken);
        var copied = input.CopyFromReportId is { } copyFrom ? await queries.DocumentAsync(copyFrom, cancellationToken) : null;
        long? templateVersionId = copied is not null
            ? copied.ReportDimensionVersionId
            : input.StartFromDimensionId is { } templateId ? (await dimensionKeys.ReadAsync(templateId, liveSnapshotId, cancellationToken))!.Version.DimensionVersionId : null;
        var structure = await reportDimensions.CreateAsync(input.Key, input.DisplayName, templateVersionId, cancellationToken);
        var report = new ReportRecord { ReportId = ids.NextId(), ReportKey = input.Key, ReportDimensionId = structure.DimensionId };
        await reports.InsertAsync(report, cancellationToken);
        await versions.InsertAsync(
            new ReportVersionRecord
            {
                ReportVersionId = ids.NextId(),
                ReportId = report.ReportId,
                SnapshotId = liveSnapshotId,
                DisplayName = input.DisplayName,
                ReportType = input.ReportType,
                CalculationModelId = input.ModelId,
                SubtotalPosition = SubtotalPositions.Above,
                IsEnabled = false,
                DefaultSettingsJson = ReportSettingsColumns.DefaultSettingsJson,
                CreatedAt = time.GetUtcNow().UtcDateTime
            },
            cancellationToken);

        if (copied is not null)
        {
            var model = await models.LiveVersionAsync(input.ModelId, cancellationToken);
            var operations = ReportCopy.Operations(copied, await models.LoadGraphAsync(model.CalculationModelVersionId, cancellationToken));
            var commit = await changes.CommitAsync(report.ReportId, new ChangeSetRequest<ReportOperation> { Operations = operations }, cancellationToken);
            if (!commit.Succeeded) return Outcome<ReportSummary>.Refused(commit.Refusal);
        }

        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<ReportSummary>.Success(await queries.SummaryAsync(report.ReportId, cancellationToken));
    }

    /// <summary>Enables a report's live version that passes every report rule, or disables it; a change advances the semantic revision.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">Whether it is enabled.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The report's summary, or the rules it breaks, in which case it stays disabled.</returns>
    public async Task<Outcome<ReportSummary>> SetEnabledAsync(long reportId, ReportEnabledInput input, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadAsync(reportId, cancellationToken);
        var version = snapshot.Content.Version;
        if (input.IsEnabled && !version.IsEnabled && await rules.EvaluateAsync([snapshot.Content], cancellationToken) is { Count: > 0 } violations)
            return Outcome<ReportSummary>.Refused(Refusal.RulesViolated("The report breaks report rules, so it stays disabled.", violations));
        if (input.IsEnabled != version.IsEnabled)
        {
            await versions.UpdateAsync(version with { IsEnabled = input.IsEnabled }, cancellationToken);
            await semanticRevision.AdvanceAsync(cancellationToken);
        }

        return Outcome<ReportSummary>.Success(await queries.SummaryAsync(reportId, cancellationToken));
    }
}
