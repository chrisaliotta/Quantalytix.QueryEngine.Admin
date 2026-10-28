using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Deletes reports with everything they own (P12).</summary>
/// <param name="reports">The report table.</param>
/// <param name="dimensionVersions">Reads the report dimension's versions.</param>
/// <param name="profiles">The profile table.</param>
/// <param name="picks">The profile pick table.</param>
/// <param name="sourceVariables">The source variable table.</param>
/// <param name="leftOut">The left-out item table.</param>
/// <param name="sources">The source table with the bridges it owns.</param>
/// <param name="calculationDimensions">The calculation dimension table.</param>
/// <param name="scopes">The variable scope table.</param>
/// <param name="scenarios">The scenario table.</param>
/// <param name="rowParameters">The row parameter value table.</param>
/// <param name="lines">The overlay line table.</param>
/// <param name="columns">The column table.</param>
/// <param name="memberOptions">The member option table.</param>
/// <param name="styles">The style table.</param>
/// <param name="reportDimensions">Removes the report dimension.</param>
/// <param name="pins">Unpins the report dimension.</param>
/// <param name="owned">Reads the report dimension's version.</param>
/// <param name="semanticRevision">Advances when the engine stops reading an enabled report.</param>
public sealed class ReportRemover(
    ITableGateway<ReportRecord> reports,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<ReportSourceLeftOutRecord> leftOut,
    ReportSourceStore sources,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<ReportScenarioRecord> scenarios,
    ITableGateway<ReportRowParameterRecord> rowParameters,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportStyleRecord> styles,
    ReportDimensions reportDimensions,
    AssumptionPins pins,
    OwnedVersions owned,
    SemanticRevision semanticRevision)
{
    /// <summary>
    /// Deletes a report with everything it owns: its content, its sources with their bridges, the report, then its report dimension,
    /// unpinned first. Nothing outside the report names what it owns, so nothing blocks the delete; the engine stops reading an
    /// enabled report, so its delete advances the semantic revision.
    /// </summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the report is gone.</returns>
    public async Task DeleteAsync(long reportId, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { ReportId = reportId }, cancellationToken);
        var owner = new { ReportId = reportId };
        var structure = await owned.StructureAsync(report, cancellationToken);
        long[] sourceIds = [.. (await sources.ListAsync(owner, cancellationToken)).Select(source => source.ReportSourceId)];
        long[] profileIds = [.. (await profiles.ListAsync(owner, cancellationToken)).Select(profile => profile.CalculationSourceProfileId)];
        await picks.DeleteAsync(new { CalculationSourceProfileId = profileIds }, cancellationToken);
        await profiles.DeleteAsync(owner, cancellationToken);
        await sourceVariables.DeleteAsync(new { ReportSourceId = sourceIds }, cancellationToken);
        await leftOut.DeleteAsync(new { ReportSourceId = sourceIds }, cancellationToken);
        await sources.DeleteAsync(owner, cancellationToken);
        await calculationDimensions.DeleteAsync(owner, cancellationToken);
        await scopes.DeleteAsync(owner, cancellationToken);
        await scenarios.DeleteAsync(owner, cancellationToken);
        await rowParameters.DeleteAsync(owner, cancellationToken);
        await lines.DeleteAsync(owner, cancellationToken);
        await columns.DeleteAsync(owner, cancellationToken);
        await memberOptions.DeleteAsync(owner, cancellationToken);
        await styles.DeleteAsync(owner, cancellationToken);
        await reports.DeleteAsync(owner, cancellationToken);
        await pins.UnpinDimensionAsync(report.ReportDimensionId, cancellationToken);
        await reportDimensions.RemoveAsync(report.ReportDimensionId, await dimensionVersions.ListAsync(new { structure.DimensionId }, cancellationToken), cancellationToken);
        if (report.IsEnabled) await semanticRevision.AdvanceAsync(cancellationToken);
    }
}
