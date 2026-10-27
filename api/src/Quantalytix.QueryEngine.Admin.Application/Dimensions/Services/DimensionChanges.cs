using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The dimension change set (OI-01) behind <c>dimensions/{dimensionId}/changes</c>; a report dimension's is refused (M8).</summary>
/// <param name="pipeline">Runs the dimension aggregate.</param>
/// <param name="dimensions">Reads the dimension's purpose.</param>
public sealed class DimensionChanges(
    ChangeSetPipeline<DimensionSnapshot, DimensionOperation, DimensionEditState> pipeline,
    ITableGateway<DimensionRecord> dimensions)
{
    /// <summary>Applies the operations in memory and writes nothing.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pending state and findings, or the refusal.</returns>
    public async Task<Outcome<ChangeSetPreview<DimensionEditState>>> PreviewAsync(
        long dimensionId,
        ChangeSetRequest<DimensionOperation> request,
        CancellationToken cancellationToken)
    {
        if (await OwnedByReportAsync(dimensionId, cancellationToken) is { } refusal) return Outcome<ChangeSetPreview<DimensionEditState>>.Refused(refusal);
        return await pipeline.PreviewAsync(dimensionId, request, cancellationToken);
    }

    /// <summary>Applies the operations to the dimension as stored now and writes the result.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored state and id map, or the refusal with nothing written.</returns>
    public async Task<Outcome<ChangeSetCommit<DimensionEditState>>> CommitAsync(
        long dimensionId,
        ChangeSetRequest<DimensionOperation> request,
        CancellationToken cancellationToken)
    {
        if (await OwnedByReportAsync(dimensionId, cancellationToken) is { } refusal) return Outcome<ChangeSetCommit<DimensionEditState>>.Refused(refusal);
        return await pipeline.CommitAsync(dimensionId, request, cancellationToken);
    }

    /// <summary>The refusal of a report dimension, or null for an analytical one.</summary>
    private async Task<Refusal?> OwnedByReportAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var dimension = await dimensions.GetAsync(new { DimensionId = dimensionId }, cancellationToken);
        return dimension.DimensionPurpose == DimensionPurposes.Report ? DimensionRefusals.OwnedByReport(dimension) : null;
    }
}
