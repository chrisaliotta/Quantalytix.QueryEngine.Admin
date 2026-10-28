using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Deletes report content together with the rows that only it gives meaning to: the variables, profile picks and bridge of a
/// source, and the picks of a profile. Each call is ordered single statements, dependents first.
/// </summary>
public sealed class ReportContentRemover(
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<ReportScenarioRecord> scenarios,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportRowParameterRecord> rowParameters,
    ITableGateway<ReportStyleRecord> styles,
    ReportBridges bridges)
{
    /// <summary>
    /// Deletes sources with their variables and the profile picks that name them, then each bridge source's bridge, unpinned first.
    /// </summary>
    /// <param name="removed">Sources of one report.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the sources and their bridges are gone.</returns>
    public async Task RemoveSourcesAsync(IReadOnlyCollection<ReportSourceRecord> removed, CancellationToken cancellationToken)
    {
        long[] sourceIds = [.. removed.Select(source => source.ReportSourceId)];
        await picks.DeleteAsync(new { ReportSourceId = sourceIds }, cancellationToken);
        await sourceVariables.DeleteAsync(new { ReportSourceId = sourceIds }, cancellationToken);
        await sources.DeleteAsync(new { ReportSourceId = sourceIds }, cancellationToken);
        foreach (var bridgeId in removed.Select(source => source.BridgeId).OfType<long>()) await bridges.DeleteAsync(bridgeId, cancellationToken);
    }

    /// <summary>Deletes profiles with their picks.</summary>
    /// <param name="profileIds">Profiles of a report.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveProfilesAsync(IReadOnlyCollection<long> profileIds, CancellationToken cancellationToken)
    {
        await picks.DeleteAsync(new { CalculationSourceProfileId = profileIds }, cancellationToken);
        await profiles.DeleteAsync(new { CalculationSourceProfileId = profileIds }, cancellationToken);
    }

    /// <summary>
    /// Deletes every content row of a report: profiles, sources with their bridges, calculation dimensions, scopes, scenarios, rows
    /// with their parameter values, columns and styles.
    /// </summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveContentAsync(long reportId, CancellationToken cancellationToken)
    {
        var owner = new { ReportId = reportId };
        await RemoveProfilesAsync([.. (await profiles.ListAsync(owner, cancellationToken)).Select(profile => profile.CalculationSourceProfileId)], cancellationToken);
        await RemoveSourcesAsync(await sources.ListAsync(owner, cancellationToken), cancellationToken);
        await dimensions.DeleteAsync(owner, cancellationToken);
        await scopes.DeleteAsync(owner, cancellationToken);
        await scenarios.DeleteAsync(owner, cancellationToken);
        await rowParameters.DeleteAsync(owner, cancellationToken);
        await lines.DeleteAsync(owner, cancellationToken);
        await columns.DeleteAsync(owner, cancellationToken);
        await memberOptions.DeleteAsync(owner, cancellationToken);
        await styles.DeleteAsync(owner, cancellationToken);
    }
}
