using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Copies a report version's content into a new draft: its variable targets, sources, profiles with their picks, calculation
/// dimensions and routes, columns, overlay rows and member options. Every row gets a new ID and every reference between the copies
/// follows it; references that leave the version name model metrics, members, datasets, mapping sets, bridges and canonical
/// dimensions, which copy as they are.
/// </summary>
public sealed class ReportVersionCloner(
    ITableGateway<ReportVariableTargetRecord> targets,
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<CalculationRouteRecord> routes,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    IIdGenerator ids)
{
    /// <summary>Copies the source version's content into the target draft.</summary>
    /// <param name="sourceVersionId">The report version to copy.</param>
    /// <param name="targetVersionId">The new draft, already stored.</param>
    /// <param name="cancellationToken">Cancels the reads and inserts.</param>
    /// <returns>A task that completes when the content is copied.</returns>
    public async Task CloneAsync(long sourceVersionId, long targetVersionId, CancellationToken cancellationToken)
    {
        var owner = new { ReportVersionId = sourceVersionId };
        var remap = new IdRemap(ids);
        var sourceProfiles = await profiles.ListAsync(owner, cancellationToken);
        await targets.InsertManyAsync(
            [.. (await targets.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = targetVersionId })],
            cancellationToken);
        await sources.InsertManyAsync(
            [.. (await sources.ListAsync(owner, cancellationToken)).Select(row => row with { VariableSourceId = remap.Map(row.VariableSourceId), ReportVersionId = targetVersionId })],
            cancellationToken);
        await profiles.InsertManyAsync(
            [.. sourceProfiles.Select(row => row with { SourceProfileId = remap.Map(row.SourceProfileId), ReportVersionId = targetVersionId })],
            cancellationToken);
        await picks.InsertManyAsync(
            [
                .. (await picks.ListAsync(new { SourceProfileId = sourceProfiles.Select(profile => profile.SourceProfileId).ToArray() }, cancellationToken))
                    .Select(row => row with { SourceProfileId = remap.Map(row.SourceProfileId), VariableSourceId = remap.Map(row.VariableSourceId) })
            ],
            cancellationToken);
        await dimensions.InsertManyAsync(
            [
                .. (await dimensions.ListAsync(owner, cancellationToken))
                    .Select(row => row with { CalculationDimensionId = remap.Map(row.CalculationDimensionId), ReportVersionId = targetVersionId })
            ],
            cancellationToken);
        await routes.InsertManyAsync(
            [
                .. (await routes.ListAsync(owner, cancellationToken)).Select(row => row with
                {
                    CalculationBindingId = remap.Map(row.CalculationBindingId),
                    ReportVersionId = targetVersionId,
                    CalculationDimensionId = remap.Map(row.CalculationDimensionId),
                    VariableSourceId = remap.MapOptional(row.VariableSourceId)
                })
            ],
            cancellationToken);
        await columns.InsertManyAsync(
            [.. (await columns.ListAsync(owner, cancellationToken)).Select(row => row with { ReportColumnId = remap.Map(row.ReportColumnId), ReportVersionId = targetVersionId })],
            cancellationToken);
        await lines.InsertManyAsync(
            [
                .. (await lines.ListAsync(owner, cancellationToken)).Select(row => row with
                {
                    ReportLineId = remap.Map(row.ReportLineId), ReportVersionId = targetVersionId, ValueColumnId = remap.MapOptional(row.ValueColumnId)
                })
            ],
            cancellationToken);
        await memberOptions.InsertManyAsync(
            [.. (await memberOptions.ListAsync(owner, cancellationToken)).Select(row => row with { ReportVersionId = targetVersionId })],
            cancellationToken);
    }
}
