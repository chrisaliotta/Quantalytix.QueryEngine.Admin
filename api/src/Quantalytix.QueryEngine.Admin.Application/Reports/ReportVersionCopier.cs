using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Copies a report version, its settings and content included, into a snapshot, for capture and restore. Line, column, source, profile
/// and calculation dimension ids are remapped through one <see cref="IdRemap"/>, so a formula row's column, a source's variables and
/// left-out items and a profile's picks point at the copies; the report dimension, roles, datasets and report bridges are identities
/// and keep their ids.
/// </summary>
public sealed class ReportVersionCopier(
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportRowParameterRecord> rowParameters,
    ITableGateway<ReportStyleRecord> styles,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<ReportScenarioRecord> scenarios,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<ReportSourceLeftOutRecord> leftOut,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ReportVersionRemover remover,
    IIdGenerator ids,
    TimeProvider time) : IVersionCopier
{
    /// <summary><c>report</c>.</summary>
    public string Kind => SnapshotKinds.Report;

    /// <summary>Copies a version into a snapshot, as a new version or over another version of the report.</summary>
    /// <param name="versionId">The version to copy.</param>
    /// <param name="targetSnapshotId">The snapshot the copy belongs to.</param>
    /// <param name="targetVersionId">The version whose settings and content the copy replaces, or null for a new version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copy's id.</returns>
    public async Task<long> CopyAsync(long versionId, long targetSnapshotId, long? targetVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(new { ReportVersionId = versionId }, cancellationToken);
        var copy = source with { ReportVersionId = targetVersionId ?? ids.NextId(), SnapshotId = targetSnapshotId, CreatedAt = time.GetUtcNow().UtcDateTime };
        if (targetVersionId is { } target)
        {
            await remover.RemoveContentAsync(target, cancellationToken);
            await versions.UpdateAsync(copy, cancellationToken);
        }
        else
        {
            await versions.InsertAsync(copy, cancellationToken);
        }

        await CopyContentAsync(versionId, copy.ReportVersionId, cancellationToken);
        return copy.ReportVersionId;
    }

    /// <summary>Copies every content row of a version into another, parents before the rows that reference them.</summary>
    private async Task CopyContentAsync(long versionId, long copyId, CancellationToken cancellationToken)
    {
        var remap = new IdRemap(ids);
        var owner = new { ReportVersionId = versionId };
        var sourceRows = await sources.ListAsync(owner, cancellationToken);
        var profileRows = await profiles.ListAsync(owner, cancellationToken);
        var sourceFilter = new { ReportSourceId = sourceRows.Select(row => row.ReportSourceId).ToArray() };

        await columns.InsertManyAsync(
            [.. (await columns.ListAsync(owner, cancellationToken)).Select(row => row with { ReportColumnId = remap.Map(row.ReportColumnId), ReportVersionId = copyId })],
            cancellationToken);
        await lines.InsertManyAsync(
            [
                .. (await lines.ListAsync(owner, cancellationToken)).Select(row => row with
                {
                    ReportLineId = remap.Map(row.ReportLineId),
                    ReportVersionId = copyId,
                    ValueReportColumnId = remap.MapOptional(row.ValueReportColumnId)
                })
            ],
            cancellationToken);
        await memberOptions.InsertManyAsync([.. (await memberOptions.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = copyId })], cancellationToken);
        await rowParameters.InsertManyAsync([.. (await rowParameters.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = copyId })], cancellationToken);
        await styles.InsertManyAsync([.. (await styles.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = copyId })], cancellationToken);
        await scopes.InsertManyAsync([.. (await scopes.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = copyId })], cancellationToken);
        await scenarios.InsertManyAsync([.. (await scenarios.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = copyId })], cancellationToken);
        await calculationDimensions.InsertManyAsync(
            [.. (await calculationDimensions.ListAsync(owner, cancellationToken)).Select(row => row with { CalculationDimensionId = remap.Map(row.CalculationDimensionId), ReportVersionId = copyId })],
            cancellationToken);
        await sources.InsertManyAsync([.. sourceRows.Select(row => row with { ReportSourceId = remap.Map(row.ReportSourceId), ReportVersionId = copyId })], cancellationToken);
        await sourceVariables.InsertManyAsync(
            [.. (await sourceVariables.ListAsync(sourceFilter, cancellationToken)).Select(row => row with { ReportSourceId = remap.Map(row.ReportSourceId) })],
            cancellationToken);
        await leftOut.InsertManyAsync(
            [.. (await leftOut.ListAsync(sourceFilter, cancellationToken)).Select(row => row with { ReportSourceId = remap.Map(row.ReportSourceId) })],
            cancellationToken);
        await profiles.InsertManyAsync(
            [.. profileRows.Select(row => row with { CalculationSourceProfileId = remap.Map(row.CalculationSourceProfileId), ReportVersionId = copyId })],
            cancellationToken);
        await picks.InsertManyAsync(
            [
                .. (await picks.ListAsync(new { CalculationSourceProfileId = profileRows.Select(row => row.CalculationSourceProfileId).ToArray() }, cancellationToken))
                    .Select(row => row with { CalculationSourceProfileId = remap.Map(row.CalculationSourceProfileId), ReportSourceId = remap.Map(row.ReportSourceId) })
            ],
            cancellationToken);
    }
}
