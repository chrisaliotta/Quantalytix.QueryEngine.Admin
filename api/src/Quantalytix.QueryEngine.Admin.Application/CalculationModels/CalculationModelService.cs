using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Reads, creates, edits and deletes calculation models: the stable identities whose versions reports read figures from.</summary>
public sealed class CalculationModelService(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> versions,
    CalculationModelVersionService versionService,
    IIdGenerator ids)
{
    /// <summary>Lists every calculation model.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The models by key, ignoring case.</returns>
    public async Task<IReadOnlyList<CalculationModelRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await models.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.ModelKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one calculation model.</summary>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model.</returns>
    public Task<CalculationModelRecord> GetAsync(long modelId, CancellationToken cancellationToken)
    {
        return models.GetAsync(new { ModelId = modelId }, cancellationToken);
    }

    /// <summary>Creates a calculation model without versions.</summary>
    /// <param name="input">The new model, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored model.</returns>
    public async Task<CalculationModelRecord> CreateAsync(NewCalculationModelInput input, CancellationToken cancellationToken)
    {
        var model = new CalculationModelRecord
        {
            ModelId = ids.NextId(), ModelKey = input.ModelKey, DisplayName = input.DisplayName, IsEnabled = input.IsEnabled
        };
        await models.InsertAsync(model, cancellationToken);
        return model;
    }

    /// <summary>Edits a model's display name and availability. Reports pin model versions, so neither reaches queries.</summary>
    /// <param name="modelId">The model.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored model.</returns>
    public async Task<CalculationModelRecord> UpdateAsync(long modelId, CalculationModelInput input, CancellationToken cancellationToken)
    {
        var updated = await GetAsync(modelId, cancellationToken) with { DisplayName = input.DisplayName, IsEnabled = input.IsEnabled };
        await models.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Deletes a model whose versions are all drafts that no report reads, with those drafts and their content.</summary>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the model was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long modelId, CancellationToken cancellationToken)
    {
        var model = await GetAsync(modelId, cancellationToken);
        var modelVersions = await versions.ListAsync(new { ModelId = modelId }, cancellationToken);
        long[] versionIds = [.. modelVersions.Select(version => version.ModelVersionId)];
        var blockers = new DeleteBlockers($"Calculation model '{model.ModelKey}'");
        blockers.AddNamed(
            "it has non-draft versions",
            modelVersions.Where(version => version.Status != PublicationStatuses.Draft).Select(version => $"v{version.VersionNumber} ({version.Status})"));
        blockers.AddNamed("read by report versions", await versionService.ReportReadersAsync(versionIds, cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        await versionService.RemoveAsync(versionIds, cancellationToken);
        await models.DeleteAsync(new { ModelId = modelId }, cancellationToken);
        return null;
    }
}
