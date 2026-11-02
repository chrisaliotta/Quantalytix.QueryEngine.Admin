using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The model change-set aggregate (blueprint 15): the live version row, its variables, calculated measures, parameters and
/// attributions, saved together. The live version is what the engine reads, so <see cref="ModelGateRule"/> refuses any operation that
/// introduces a model-rule or report-rule violation before anything is written (C7, OI-42).
/// </summary>
/// <param name="loader">Reads the model and the reports over it.</param>
/// <param name="models">The model version table.</param>
/// <param name="variables">The variable table.</param>
/// <param name="measures">The calculated measure table.</param>
/// <param name="parameters">The parameter table.</param>
/// <param name="attributions">The attribution table.</param>
/// <param name="drivers">The attribution driver table.</param>
public sealed class ModelChanges(
    ModelSnapshotLoader loader,
    ITableGateway<CalculationModelVersionRecord> models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> measures,
    ITableGateway<ModelParameterRecord> parameters,
    ITableGateway<MetricAttributionRecord> attributions,
    ITableGateway<MetricAttributionDriverRecord> drivers)
    : IChangeSetAggregate<ModelSnapshot, ModelOperation, ModelDocument>
{
    /// <summary>The depth of the model's content rows below the model row.</summary>
    private const int ContentDepth = 1;

    /// <summary>The depth of the attribution drivers, below their attribution.</summary>
    private const int DriverDepth = 2;

    /// <summary>Reads the model's live version with the reports over it.</summary>
    /// <param name="aggregateId">The model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public Task<ModelSnapshot> LoadAsync(long aggregateId, CancellationToken cancellationToken)
    {
        return loader.LoadLiveAsync(aggregateId, cancellationToken);
    }

    /// <summary>Applies one operation to the model's content.</summary>
    /// <param name="snapshot">The aggregate so far.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Allocates ids and takes refusals.</param>
    /// <returns>The aggregate with the operation applied.</returns>
    public ModelSnapshot Apply(ModelSnapshot snapshot, ModelOperation operation, ChangeContext context)
    {
        return snapshot with { Content = ModelEdits.Apply(snapshot.Content, operation, context) };
    }

    /// <summary>Plans the row writes, the version row last.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <param name="plan">The plan.</param>
    public void Plan(ModelSnapshot before, ModelSnapshot after, WritePlan plan)
    {
        var stored = ModelTableRows.Of(before.Content);
        var changed = ModelTableRows.Of(after.Content);
        if (stored.Model != changed.Model) plan.Update(models, WritePlan.Root, changed.Model);
        plan.Sync(variables, ContentDepth, stored.Variables, changed.Variables, row => new { row.VariableId });
        plan.Sync(measures, ContentDepth, stored.Measures, changed.Measures, row => new { row.CalculatedMeasureId });
        plan.Sync(parameters, ContentDepth, stored.Parameters, changed.Parameters, row => new { row.CalculationModelVersionId, row.ParameterKey });
        plan.Sync(attributions, ContentDepth, stored.Attributions, changed.Attributions, row => new { row.CalculationModelVersionId, row.MetricKey });
        plan.Sync(drivers, DriverDepth, stored.Drivers, changed.Drivers, row => new { row.CalculationModelVersionId, row.MetricKey, row.DriverKey });
    }

    /// <summary>The document of a snapshot; the model change set creates no temporary ids, so every id is stored.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="presentId">Unused: model items are named by key.</param>
    /// <param name="cancellationToken">Unused; the snapshot holds everything.</param>
    /// <returns>The document.</returns>
    public Task<ModelDocument> ReadAsync(ModelSnapshot snapshot, Func<long, string> presentId, CancellationToken cancellationToken)
    {
        return Task.FromResult(ModelReadModels.Document(snapshot.Content, snapshot.Surroundings.LiveSnapshotId, snapshot.Surroundings.Reports.Select(report => new VersionedReport(report.Report, report.Version))));
    }
}
