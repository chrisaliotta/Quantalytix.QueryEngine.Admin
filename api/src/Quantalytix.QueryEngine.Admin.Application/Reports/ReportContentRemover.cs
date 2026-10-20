using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Deletes the content rows of editable report versions together with the rows that only they give meaning to: the profile picks
/// of a source or a profile. Each call is ordered single statements, dependents first.
/// </summary>
public sealed class ReportContentRemover(
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportMemberOptionRecord> memberOptions)
{
    /// <summary>Deletes sources with the profile picks that name them.</summary>
    /// <param name="sourceIds">Sources of editable versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveSourcesAsync(IReadOnlyCollection<long> sourceIds, CancellationToken cancellationToken)
    {
        await picks.DeleteAsync(new { VariableSourceId = sourceIds }, cancellationToken);
        await sources.DeleteAsync(new { VariableSourceId = sourceIds }, cancellationToken);
    }

    /// <summary>Deletes profiles with their picks.</summary>
    /// <param name="profileIds">Profiles of editable versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveProfilesAsync(IReadOnlyCollection<long> profileIds, CancellationToken cancellationToken)
    {
        await picks.DeleteAsync(new { SourceProfileId = profileIds }, cancellationToken);
        await profiles.DeleteAsync(new { SourceProfileId = profileIds }, cancellationToken);
    }

    /// <summary>Deletes every content row of draft versions.</summary>
    /// <param name="versionIds">Draft report versions.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveContentAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        var owners = new { ReportVersionId = versionIds };
        var versionProfiles = await profiles.ListAsync(owners, cancellationToken);
        await RemoveProfilesAsync([.. versionProfiles.Select(profile => profile.SourceProfileId)], cancellationToken);
        await sources.DeleteAsync(owners, cancellationToken);
        await dimensions.DeleteAsync(owners, cancellationToken);
        await scopes.DeleteAsync(owners, cancellationToken);
        await lines.DeleteAsync(owners, cancellationToken);
        await columns.DeleteAsync(owners, cancellationToken);
        await memberOptions.DeleteAsync(owners, cancellationToken);
    }
}
