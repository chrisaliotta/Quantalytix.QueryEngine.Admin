using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The report change set (OI-01, OI-46) behind <c>reports/{reportId}/changes</c>. A preview answers the pending report's diagnostics
/// with it, so readiness, coverage and resolution render before Save (OI-05).
/// </summary>
/// <param name="pipeline">Runs the report aggregate.</param>
/// <param name="diagnostics">Computes the pending report's diagnostics.</param>
public sealed class ReportChanges(ChangeSetPipeline<ReportSnapshot, ReportOperation, ReportDocument> pipeline, ReportDiagnosticsBuilder diagnostics)
{
    /// <summary>Applies the operations in memory, diagnoses the result, and writes nothing.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pending report with its findings and diagnostics, or the refusal.</returns>
    public async Task<Outcome<ReportChangeSetPreview>> PreviewAsync(long reportId, ChangeSetRequest<ReportOperation> request, CancellationToken cancellationToken)
    {
        var preview = await pipeline.PreviewAsync(reportId, request, cancellationToken);
        if (!preview.Succeeded) return Outcome<ReportChangeSetPreview>.Refused(preview.Refusal);
        return Outcome<ReportChangeSetPreview>.Success(new ReportChangeSetPreview
        {
            State = preview.Value.State,
            Violations = preview.Value.Violations,
            Diagnostics = await diagnostics.BuildAsync(preview.Value.State, preview.Value.Violations, cancellationToken)
        });
    }

    /// <summary>Applies the operations to the report as stored now and writes the result.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored report, or the refusal with nothing written.</returns>
    public Task<Outcome<ChangeSetCommit<ReportDocument>>> CommitAsync(long reportId, ChangeSetRequest<ReportOperation> request, CancellationToken cancellationToken)
    {
        return pipeline.CommitAsync(reportId, request, cancellationToken);
    }
}
