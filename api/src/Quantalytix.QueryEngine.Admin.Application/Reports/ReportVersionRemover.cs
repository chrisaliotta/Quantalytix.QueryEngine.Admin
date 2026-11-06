using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Hard-deletes report versions with their content, children before parents and the version row last, and a report once no snapshot
/// holds a version of it. The report dimension and report bridges are kinds of their own, removed by their own removers.
/// </summary>
public sealed class ReportVersionRemover(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<ReportSourceLeftOutRecord> leftOut,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<ReportScenarioRecord> scenarios,
    ITableGateway<ReportRowParameterRecord> rowParameters,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportStyleRecord> styles) : IVersionRemover
{
    /// <summary><c>report</c>.</summary>
    public string Kind => SnapshotKinds.Report;

    /// <summary>Deletes the versions' content, then the versions.</summary>
    /// <param name="versionIds">The report versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveVersionsAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await RemoveContentAsync(versionIds, cancellationToken);
        await versions.DeleteAsync(new { ReportVersionId = versionIds }, cancellationToken);
    }

    /// <summary>Deletes the versions' content and keeps the version rows, which a copy then fills again.</summary>
    /// <param name="versionIds">The report versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the content is gone.</returns>
    public async Task RemoveContentAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        var owner = new { ReportVersionId = versionIds };
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
    }

    /// <summary>Deletes report rows.</summary>
    /// <param name="identityIds">The reports.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveIdentitiesAsync(IReadOnlyCollection<long> identityIds, CancellationToken cancellationToken)
    {
        await reports.DeleteAsync(new { ReportId = identityIds }, cancellationToken);
    }
}
