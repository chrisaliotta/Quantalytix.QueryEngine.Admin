using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The model manager's reads: the list of the models the live snapshot holds, a live or frozen version's document, a formula check
/// against the saved live version, and the delete check.
/// </summary>
/// <param name="models">The model identities.</param>
/// <param name="versions">The model versions.</param>
/// <param name="variables">The variable table.</param>
/// <param name="measures">The calculated measure table.</param>
/// <param name="loader">Reads a model version's content and readers.</param>
/// <param name="live">The live snapshot.</param>
/// <param name="holders">Names the frozen snapshots that hold a model.</param>
/// <param name="reports">Reads the live reports.</param>
public sealed class ModelQueries(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> versions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> measures,
    ModelSnapshotLoader loader,
    LiveSnapshot live,
    SnapshotHolders holders,
    ReportVersionReader reports)
{
    /// <summary>Lists every model the live snapshot holds with its live version's metric counts and readers.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items by key, ignoring case.</returns>
    public async Task<IReadOnlyList<ModelListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var liveSnapshotId = await live.IdAsync(cancellationToken);
        var liveVersions = (await versions.ListAsync(new { SnapshotId = liveSnapshotId }, cancellationToken)).ToDictionary(version => version.CalculationModelId);
        long[] versionIds = [.. liveVersions.Values.Select(version => version.CalculationModelVersionId)];
        var variableCounts = (await variables.ListAsync(new { CalculationModelVersionId = versionIds }, cancellationToken)).CountBy(variable => variable.CalculationModelVersionId).ToDictionary();
        var measureCounts = (await measures.ListAsync(new { CalculationModelVersionId = versionIds }, cancellationToken)).CountBy(measure => measure.CalculationModelVersionId).ToDictionary();
        var readers = (await reports.InSnapshotAsync(liveSnapshotId, null, cancellationToken)).ToLookup(report => report.Version.CalculationModelId);
        return
        [
            .. (await models.ListAsync(new { CalculationModelId = liveVersions.Keys.ToArray() }, cancellationToken))
                .OrderBy(model => model.ModelKey, StringComparer.OrdinalIgnoreCase)
                .Select(model =>
                {
                    var version = liveVersions[model.CalculationModelId];
                    return new ModelListItem(
                        ModelReadModels.Settings(model, version, liveSnapshotId),
                        variableCounts.GetValueOrDefault(version.CalculationModelVersionId),
                        measureCounts.GetValueOrDefault(version.CalculationModelVersionId),
                        [.. readers[model.CalculationModelId].Select(ModelReadModels.Use)]);
                })
        ];
    }

    /// <summary>Reads a model's live document.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The document.</returns>
    public async Task<ModelDocument> DocumentAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        return await VersionDocumentAsync((await loader.LiveVersionAsync(calculationModelId, cancellationToken)).CalculationModelVersionId, cancellationToken);
    }

    /// <summary>Reads a model version's document, live or frozen.</summary>
    /// <param name="modelVersionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The document.</returns>
    public async Task<ModelDocument> VersionDocumentAsync(long modelVersionId, CancellationToken cancellationToken)
    {
        var content = await loader.LoadGraphAsync(modelVersionId, cancellationToken);
        return ModelReadModels.Document(content, await live.IdAsync(cancellationToken), await loader.ReadersAsync(content.Version, cancellationToken));
    }

    /// <summary>Checks a formula against the saved live version.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="request">The formula and the metric that owns it.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<ModelFormulaCheck> CheckFormulaAsync(long calculationModelId, ModelFormulaCheckRequest request, CancellationToken cancellationToken)
    {
        var version = await loader.LiveVersionAsync(calculationModelId, cancellationToken);
        return ModelFormulaChecker.Check(await loader.LoadGraphAsync(version.CalculationModelVersionId, cancellationToken), request.Formula, request.SelfKey);
    }

    /// <summary>Whether a model can be deleted.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<DeleteCheck> DeleteCheckAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        var model = await models.GetAsync(new { CalculationModelId = calculationModelId }, cancellationToken);
        var all = await versions.ListAsync(new { CalculationModelId = calculationModelId }, cancellationToken);
        return ModelDeleteRules.Model(
            model,
            await loader.ReadersAsync(await loader.LiveVersionAsync(calculationModelId, cancellationToken), cancellationToken),
            await holders.FrozenKeysAsync(all.Select(row => row.SnapshotId), cancellationToken));
    }
}
