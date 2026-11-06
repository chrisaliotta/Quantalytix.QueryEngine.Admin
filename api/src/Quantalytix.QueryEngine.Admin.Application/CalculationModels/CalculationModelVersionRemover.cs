using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Hard-deletes calculation model versions with their content, drivers before attributions and the version rows last, and models once
/// no snapshot holds a version of them.
/// </summary>
public sealed class CalculationModelVersionRemover(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> versions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> measures,
    ITableGateway<ModelParameterRecord> parameters,
    ITableGateway<MetricAttributionRecord> attributions,
    ITableGateway<MetricAttributionDriverRecord> drivers) : IVersionRemover
{
    /// <summary><c>calculation_model</c>.</summary>
    public string Kind => SnapshotKinds.CalculationModel;

    /// <summary>Deletes the versions' content, then the versions.</summary>
    /// <param name="versionIds">The model versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveVersionsAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await RemoveContentAsync(versionIds, cancellationToken);
        await versions.DeleteAsync(new { CalculationModelVersionId = versionIds }, cancellationToken);
    }

    /// <summary>Deletes the versions' content and keeps the version rows, which a copy then fills again.</summary>
    /// <param name="versionIds">The model versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the content is gone.</returns>
    public async Task RemoveContentAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        var owner = new { CalculationModelVersionId = versionIds };
        await drivers.DeleteAsync(owner, cancellationToken);
        await attributions.DeleteAsync(owner, cancellationToken);
        await variables.DeleteAsync(owner, cancellationToken);
        await measures.DeleteAsync(owner, cancellationToken);
        await parameters.DeleteAsync(owner, cancellationToken);
    }

    /// <summary>Deletes model rows.</summary>
    /// <param name="identityIds">The models.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveIdentitiesAsync(IReadOnlyCollection<long> identityIds, CancellationToken cancellationToken)
    {
        await models.DeleteAsync(new { CalculationModelId = identityIds }, cancellationToken);
    }
}
