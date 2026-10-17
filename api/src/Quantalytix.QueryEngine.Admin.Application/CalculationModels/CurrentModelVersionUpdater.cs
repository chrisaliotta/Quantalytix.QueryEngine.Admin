using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Points <c>qe_calculation_model.current_model_version_id</c> at a model version when it is published. The pointer is
/// authoring-only: report versions pin model versions themselves, so it changes nothing reports read.
/// </summary>
public sealed class CurrentModelVersionUpdater(
    ITableGateway<CalculationModelVersionRecord> versions,
    ITableGateway<CalculationModelRecord> models)
    : IVersionLifecycleHandler
{
    /// <summary>The kind whose lifecycle the updater joins: calculation model versions.</summary>
    public string Kind { get; } = VersionKinds.CalculationModelVersion;

    /// <summary>Validation derives nothing here.</summary>
    /// <param name="versionId">The model version being validated.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task OnValidatingAsync(long versionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>Makes the newly published version its model's current version.</summary>
    /// <param name="versionId">The newly published model version.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>A task that completes when the pointer is written.</returns>
    public async Task OnPublishedAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { ModelVersionId = versionId }, cancellationToken);
        var model = await models.GetAsync(new { version.ModelId }, cancellationToken);
        await models.UpdateAsync(model with { CurrentModelVersionId = versionId }, cancellationToken);
    }
}
