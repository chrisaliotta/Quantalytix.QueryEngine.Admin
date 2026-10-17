using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Deletes the content rows of draft model versions together with the rows that only they give meaning to: a variable's
/// sources, a source's profile picks and override routes, a profile's picks and a calculation dimension's routes. Each call
/// is ordered single statements, dependents first.
/// </summary>
public sealed class ModelContentRemover(
    ITableGateway<VariableRecord> variables,
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<CalculationRouteRecord> routes)
{
    /// <summary>Deletes variables with their sources and picks.</summary>
    /// <param name="variableIds">Variables of draft versions.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveVariablesAsync(IReadOnlyCollection<long> variableIds, CancellationToken cancellationToken)
    {
        var owned = await sources.ListAsync(new { VariableId = variableIds }, cancellationToken);
        await RemoveSourcesAsync([.. owned.Select(source => source.VariableSourceId)], cancellationToken);
        await picks.DeleteAsync(new { VariableId = variableIds }, cancellationToken);
        await variables.DeleteAsync(new { VariableId = variableIds }, cancellationToken);
    }

    /// <summary>Deletes sources with the profile picks and override routes that name them.</summary>
    /// <param name="sourceIds">Sources of draft versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveSourcesAsync(IReadOnlyCollection<long> sourceIds, CancellationToken cancellationToken)
    {
        await picks.DeleteAsync(new { VariableSourceId = sourceIds }, cancellationToken);
        await routes.DeleteAsync(new { VariableSourceId = sourceIds }, cancellationToken);
        await sources.DeleteAsync(new { VariableSourceId = sourceIds }, cancellationToken);
    }

    /// <summary>Deletes profiles with their picks.</summary>
    /// <param name="profileIds">Profiles of draft versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveProfilesAsync(IReadOnlyCollection<long> profileIds, CancellationToken cancellationToken)
    {
        await picks.DeleteAsync(new { SourceProfileId = profileIds }, cancellationToken);
        await profiles.DeleteAsync(new { SourceProfileId = profileIds }, cancellationToken);
    }

    /// <summary>Deletes calculation dimensions with their routes.</summary>
    /// <param name="dimensionIds">Calculation dimensions of draft versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveDimensionsAsync(IReadOnlyCollection<long> dimensionIds, CancellationToken cancellationToken)
    {
        await routes.DeleteAsync(new { CalculationDimensionId = dimensionIds }, cancellationToken);
        await dimensions.DeleteAsync(new { CalculationDimensionId = dimensionIds }, cancellationToken);
    }

    /// <summary>Deletes every content row of draft versions.</summary>
    /// <param name="versionIds">Draft model versions.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task RemoveContentAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        var versionProfiles = await profiles.ListAsync(new { ModelVersionId = versionIds }, cancellationToken);
        await RemoveProfilesAsync([.. versionProfiles.Select(profile => profile.SourceProfileId)], cancellationToken);
        await routes.DeleteAsync(new { ModelVersionId = versionIds }, cancellationToken);
        await sources.DeleteAsync(new { ModelVersionId = versionIds }, cancellationToken);
        await variables.DeleteAsync(new { ModelVersionId = versionIds }, cancellationToken);
        await calculatedMeasures.DeleteAsync(new { ModelVersionId = versionIds }, cancellationToken);
        await dimensions.DeleteAsync(new { ModelVersionId = versionIds }, cancellationToken);
    }
}
