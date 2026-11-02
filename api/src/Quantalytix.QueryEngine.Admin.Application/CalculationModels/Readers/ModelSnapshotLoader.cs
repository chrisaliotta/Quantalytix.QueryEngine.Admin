using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Reads a calculation model version as stored: its content alone for the reads, and for a change set or restore's rules also every
/// report over it in the version's snapshot, read once through the reports module's <see cref="ReportGraphReader"/>, with the
/// violations the stored version has.
/// </summary>
/// <param name="models">The model identities.</param>
/// <param name="versions">The model versions.</param>
/// <param name="variables">The variable table.</param>
/// <param name="measures">The calculated measure table.</param>
/// <param name="attributions">Reads the attributions with their drivers.</param>
/// <param name="parameters">Reads the parameters.</param>
/// <param name="reports">Reads the reports over the model.</param>
/// <param name="reader">Reads each report as it stands, and what it is checked against.</param>
/// <param name="live">The live snapshot.</param>
public sealed class ModelSnapshotLoader(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> versions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> measures,
    MetricAttributionService attributions,
    ModelParameterService parameters,
    ReportVersionReader reports,
    ReportGraphReader reader,
    LiveSnapshot live)
{
    /// <summary>The live version of a model.</summary>
    /// <param name="calculationModelId">The model, which the live snapshot holds.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The version row.</returns>
    public async Task<CalculationModelVersionRecord> LiveVersionAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        return (await versions.ListAsync(new { CalculationModelId = calculationModelId, SnapshotId = await live.IdAsync(cancellationToken) }, cancellationToken)).Single();
    }

    /// <summary>Reads a model version's content.</summary>
    /// <param name="modelVersionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The model with its metrics by key, its attributions and its parameters.</returns>
    public async Task<ModelGraph> LoadGraphAsync(long modelVersionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { CalculationModelVersionId = modelVersionId }, cancellationToken);
        var owner = new { CalculationModelVersionId = modelVersionId };
        return new ModelGraph
        {
            Model = await models.GetAsync(new { version.CalculationModelId }, cancellationToken),
            Version = version,
            Variables = [.. (await variables.ListAsync(owner, cancellationToken)).OrderBy(variable => variable.VariableKey, StringComparer.Ordinal)],
            CalculatedMeasures = [.. (await measures.ListAsync(owner, cancellationToken)).OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal)],
            Attributions = await attributions.ListAsync(modelVersionId, cancellationToken),
            Parameters = await parameters.ListAsync(modelVersionId, cancellationToken)
        };
    }

    /// <summary>The reports, enabled or not, that read a model version: the reports over its model in its snapshot.</summary>
    /// <param name="version">The model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The reports by key.</returns>
    public Task<IReadOnlyList<VersionedReport>> ReadersAsync(CalculationModelVersionRecord version, CancellationToken cancellationToken)
    {
        return reports.InSnapshotAsync(version.SnapshotId, version.CalculationModelId, cancellationToken);
    }

    /// <summary>Reads a model's live version as a change set starts from.</summary>
    /// <param name="calculationModelId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<ModelSnapshot> LoadLiveAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        return await LoadVersionAsync((await LiveVersionAsync(calculationModelId, cancellationToken)).CalculationModelVersionId, cancellationToken);
    }

    /// <summary>Reads a stored model version: its content, the reports over it, and the violations it has.</summary>
    /// <param name="modelVersionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<ModelSnapshot> LoadVersionAsync(long modelVersionId, CancellationToken cancellationToken)
    {
        var content = await LoadGraphAsync(modelVersionId, cancellationToken);
        var graphs = new List<ReportGraph>();
        foreach (var report in await ReadersAsync(content.Version, cancellationToken)) graphs.Add(await reader.ReadAsync(report, cancellationToken));
        var stored = new ModelSnapshot
        {
            Content = content,
            Surroundings = new ModelSurroundings
            {
                LiveSnapshotId = await live.IdAsync(cancellationToken),
                Reports = graphs,
                Scope = graphs.Count > 0 ? await reader.ScopeAsync(graphs, content.Version.SnapshotId, cancellationToken) : null,
                Baseline = new HashSet<RuleViolation>()
            }
        };
        return stored with { Surroundings = stored.Surroundings with { Baseline = ModelChecks.Violations(stored).ToHashSet() } };
    }
}
