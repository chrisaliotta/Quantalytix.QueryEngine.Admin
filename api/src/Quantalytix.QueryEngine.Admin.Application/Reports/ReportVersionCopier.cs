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

    /// <summary>Copies versions into a snapshot, each as a new version or over another version of its report.</summary>
    /// <param name="copies">The versions to copy, each with the version whose settings and content it replaces, if any.</param>
    /// <param name="targetSnapshotId">The snapshot the copies belong to.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copies' ids.</returns>
    public async Task<IReadOnlyList<long>> CopyAsync(IReadOnlyList<VersionCopy> copies, long targetSnapshotId, CancellationToken cancellationToken)
    {
        var batch = new VersionCopyBatch(copies, ids);
        var now = time.GetUtcNow().UtcDateTime;
        var sources = await versions.ListAsync(new { ReportVersionId = batch.SourceIds }, cancellationToken);
        await remover.RemoveContentAsync(batch.ReplacedIds, cancellationToken);
        await versions.InsertManyAsync(
            [.. sources.Select(source => source with { ReportVersionId = batch.Targets[source.ReportVersionId], SnapshotId = targetSnapshotId, CreatedAt = now })],
            cancellationToken);
        await CopyContentAsync(batch.Targets, cancellationToken);
        return batch.CopyIds;
    }

    /// <summary>
    /// The version's settings and content, every row by its key: a formula row's column, a source's variables and left-out items, and
    /// a profile's picks name the column, source and profile keys in place of their ids.
    /// </summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content key.</returns>
    public async Task<string> ContentKeyAsync(long versionId, CancellationToken cancellationToken)
    {
        var owner = new { ReportVersionId = versionId };
        var version = await versions.GetAsync(owner, cancellationToken);
        var columnRows = await columns.ListAsync(owner, cancellationToken);
        var sourceRows = await sources.ListAsync(owner, cancellationToken);
        var profileRows = await profiles.ListAsync(owner, cancellationToken);
        var columnKeys = columnRows.ToDictionary(row => row.ReportColumnId, row => row.ColumnKey);
        var sourceKeys = sourceRows.ToDictionary(row => row.ReportSourceId, row => row.SourceKey);
        var profileKeys = profileRows.ToDictionary(row => row.CalculationSourceProfileId, row => row.ProfileKey);
        var sourceFilter = new { ReportSourceId = sourceKeys.Keys.ToArray() };
        return ContentKey.Of(
            [version with { ReportVersionId = 0, SnapshotId = 0, CreatedAt = default }],
            columnRows.Select(row => row with { ReportColumnId = 0, ReportVersionId = 0 }),
            (await lines.ListAsync(owner, cancellationToken)).Select(row => new
            {
                Line = row with { ReportLineId = 0, ReportVersionId = 0, ValueReportColumnId = null },
                ValueColumn = ContentKey.KeyOf(columnKeys, row.ValueReportColumnId)
            }),
            (await memberOptions.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = 0 }),
            (await rowParameters.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = 0 }),
            (await styles.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = 0 }),
            (await scopes.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = 0 }),
            (await scenarios.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = 0 }),
            (await calculationDimensions.ListAsync(owner, cancellationToken)).Select(row => row with { CalculationDimensionId = 0, ReportVersionId = 0 }),
            sourceRows.Select(row => row with { ReportSourceId = 0, ReportVersionId = 0 }),
            (await sourceVariables.ListAsync(sourceFilter, cancellationToken)).Select(row => new { Variable = row with { ReportSourceId = 0 }, Source = sourceKeys[row.ReportSourceId] }),
            (await leftOut.ListAsync(sourceFilter, cancellationToken)).Select(row => new { row.MemberKey, Source = sourceKeys[row.ReportSourceId] }),
            profileRows.Select(row => row with { CalculationSourceProfileId = 0, ReportVersionId = 0 }),
            (await picks.ListAsync(new { CalculationSourceProfileId = profileKeys.Keys.ToArray() }, cancellationToken))
                .Select(row => new { row.VariableKey, Profile = profileKeys[row.CalculationSourceProfileId], Source = sourceKeys[row.ReportSourceId] }));
    }

    /// <summary>Copies every content row of each source version into its target, parents before the rows that reference them.</summary>
    private async Task CopyContentAsync(IReadOnlyDictionary<long, long> targets, CancellationToken cancellationToken)
    {
        var remap = new IdRemap(ids);
        var owner = new { ReportVersionId = targets.Keys.ToArray() };
        var sourceRows = await sources.ListAsync(owner, cancellationToken);
        var profileRows = await profiles.ListAsync(owner, cancellationToken);
        var sourceFilter = new { ReportSourceId = sourceRows.Select(row => row.ReportSourceId).ToArray() };

        await columns.InsertManyAsync(
            [.. (await columns.ListAsync(owner, cancellationToken)).Select(row => row with { ReportColumnId = remap.Map(row.ReportColumnId), ReportVersionId = targets[row.ReportVersionId] })],
            cancellationToken);
        await lines.InsertManyAsync(
            [
                .. (await lines.ListAsync(owner, cancellationToken)).Select(row => row with
                {
                    ReportLineId = remap.Map(row.ReportLineId),
                    ReportVersionId = targets[row.ReportVersionId],
                    ValueReportColumnId = remap.MapOptional(row.ValueReportColumnId)
                })
            ],
            cancellationToken);
        await memberOptions.InsertManyAsync([.. (await memberOptions.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = targets[row.ReportVersionId] })], cancellationToken);
        await rowParameters.InsertManyAsync([.. (await rowParameters.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = targets[row.ReportVersionId] })], cancellationToken);
        await styles.InsertManyAsync([.. (await styles.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = targets[row.ReportVersionId] })], cancellationToken);
        await scopes.InsertManyAsync([.. (await scopes.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = targets[row.ReportVersionId] })], cancellationToken);
        await scenarios.InsertManyAsync([.. (await scenarios.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = targets[row.ReportVersionId] })], cancellationToken);
        await calculationDimensions.InsertManyAsync(
            [
                .. (await calculationDimensions.ListAsync(owner, cancellationToken)).Select(row => row with
                {
                    CalculationDimensionId = remap.Map(row.CalculationDimensionId),
                    ReportVersionId = targets[row.ReportVersionId]
                })
            ],
            cancellationToken);
        await sources.InsertManyAsync([.. sourceRows.Select(row => row with { ReportSourceId = remap.Map(row.ReportSourceId), ReportVersionId = targets[row.ReportVersionId] })], cancellationToken);
        await sourceVariables.InsertManyAsync(
            [.. (await sourceVariables.ListAsync(sourceFilter, cancellationToken)).Select(row => row with { ReportSourceId = remap.Map(row.ReportSourceId) })],
            cancellationToken);
        await leftOut.InsertManyAsync(
            [.. (await leftOut.ListAsync(sourceFilter, cancellationToken)).Select(row => row with { ReportSourceId = remap.Map(row.ReportSourceId) })],
            cancellationToken);
        await profiles.InsertManyAsync(
            [.. profileRows.Select(row => row with { CalculationSourceProfileId = remap.Map(row.CalculationSourceProfileId), ReportVersionId = targets[row.ReportVersionId] })],
            cancellationToken);
        await picks.InsertManyAsync(
            [
                .. (await picks.ListAsync(new { CalculationSourceProfileId = profileRows.Select(row => row.CalculationSourceProfileId).ToArray() }, cancellationToken))
                    .Select(row => row with { CalculationSourceProfileId = remap.Map(row.CalculationSourceProfileId), ReportSourceId = remap.Map(row.ReportSourceId) })
            ],
            cancellationToken);
    }
}
