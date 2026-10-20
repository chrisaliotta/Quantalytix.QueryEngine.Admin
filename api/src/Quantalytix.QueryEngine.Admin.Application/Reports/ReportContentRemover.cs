using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Deletes the content rows of editable report versions together with the rows that only they give meaning to: a source's profile
/// picks and override routes, a profile's picks and a calculation dimension's routes. Each call is ordered single statements,
/// dependents first.
/// </summary>
public sealed class ReportContentRemover(
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<CalculationRouteRecord> routes,
    ITableGateway<ReportVariableTargetRecord> targets,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportMemberOptionRecord> memberOptions)
{
    /// <summary>Deletes sources with the profile picks and override routes that name them.</summary>
    /// <param name="sourceIds">Sources of editable versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveSourcesAsync(IReadOnlyCollection<long> sourceIds, CancellationToken cancellationToken)
    {
        await picks.DeleteAsync(new { VariableSourceId = sourceIds }, cancellationToken);
        await routes.DeleteAsync(new { VariableSourceId = sourceIds }, cancellationToken);
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

    /// <summary>Deletes calculation dimensions with their routes.</summary>
    /// <param name="dimensionIds">Calculation dimensions of editable versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveDimensionsAsync(IReadOnlyCollection<long> dimensionIds, CancellationToken cancellationToken)
    {
        await routes.DeleteAsync(new { CalculationDimensionId = dimensionIds }, cancellationToken);
        await dimensions.DeleteAsync(new { CalculationDimensionId = dimensionIds }, cancellationToken);
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
        await routes.DeleteAsync(owners, cancellationToken);
        await sources.DeleteAsync(owners, cancellationToken);
        await dimensions.DeleteAsync(owners, cancellationToken);
        await targets.DeleteAsync(owners, cancellationToken);
        await lines.DeleteAsync(owners, cancellationToken);
        await columns.DeleteAsync(owners, cancellationToken);
        await memberOptions.DeleteAsync(owners, cancellationToken);
    }
}
