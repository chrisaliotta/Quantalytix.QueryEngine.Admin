using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Hard-deletes calculation model versions with their content, drivers before attributions and the version row last, and a model once
/// no snapshot holds a version of it.
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

    /// <summary>Deletes a version's content, then the version.</summary>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        await RemoveContentAsync(versionId, cancellationToken);
        await versions.DeleteAsync(new { CalculationModelVersionId = versionId }, cancellationToken);
    }

    /// <summary>Deletes a version's content and keeps the version row, which a copy then fills again.</summary>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the content is gone.</returns>
    public async Task RemoveContentAsync(long versionId, CancellationToken cancellationToken)
    {
        var owner = new { CalculationModelVersionId = versionId };
        await drivers.DeleteAsync(owner, cancellationToken);
        await attributions.DeleteAsync(owner, cancellationToken);
        await variables.DeleteAsync(owner, cancellationToken);
        await measures.DeleteAsync(owner, cancellationToken);
        await parameters.DeleteAsync(owner, cancellationToken);
    }

    /// <summary>Deletes a model row.</summary>
    /// <param name="identityId">The model.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the row is gone.</returns>
    public async Task RemoveIdentityAsync(long identityId, CancellationToken cancellationToken)
    {
        await models.DeleteAsync(new { CalculationModelId = identityId }, cancellationToken);
    }
}
