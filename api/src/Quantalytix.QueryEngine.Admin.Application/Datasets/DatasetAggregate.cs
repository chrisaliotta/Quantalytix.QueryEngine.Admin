using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The dataset version change-set aggregate (blueprint 11): one version's settings, fields, grains, bindings and measures, and its
/// dataset's identity, saved together. Content changes need a draft (D3); a version that is not a draft accepts only
/// <c>updateDataset</c>. Only the identity is engine-visible: queries read published versions, which never change (D1, D2).
/// </summary>
public sealed class DatasetAggregate(
    DatasetSnapshotLoader loader,
    DatasetVersionReader reader,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<FieldRecord> fields,
    ITableGateway<DatasetGrainRecord> grains,
    ITableGateway<DatasetGrainFieldRecord> grainFields,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<MeasureRecord> measures,
    ITableGateway<MeasureComponentRecord> components,
    ITableGateway<MeasureDependencyRecord> dependencies,
    ITableGateway<MeasureTimePolicyRecord> timePolicies,
    ITableGateway<ManagedSourceObjectRecord> managed,
    DatasetSchemaChanges schema)
    : IChangeSetAggregate<DatasetSnapshot, DatasetOperation, DatasetVersion>
{
    /// <summary>The depth of rows that name only the version: fields, grains, measures and the managed-table registry.</summary>
    private const int ItemDepth = 1;

    /// <summary>The depth of rows that name items: bindings, grain and measure components, dependencies.</summary>
    private const int ItemReferenceDepth = 2;

    /// <summary>The depth of rows that name bindings: time axes and time policies.</summary>
    private const int BindingReferenceDepth = 3;

    /// <summary>Reads the version as stored.</summary>
    /// <param name="aggregateId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public Task<DatasetSnapshot> LoadAsync(long aggregateId, CancellationToken cancellationToken)
    {
        return loader.LoadAsync(aggregateId, cancellationToken);
    }

    /// <summary>Applies one operation.</summary>
    /// <param name="snapshot">The aggregate so far.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the operation applied.</returns>
    public DatasetSnapshot Apply(DatasetSnapshot snapshot, DatasetOperation operation, ChangeContext context)
    {
        if (operation is DatasetUpdateDatasetOperation update) return DatasetSettingsEdits.UpdateDataset(snapshot, update);
        if (!snapshot.IsDraft)
        {
            context.Conflict(
                DatasetRefusals.WrongStatusTitle,
                $"Version v{snapshot.Version.VersionNumber} is {snapshot.Version.Status}; only a draft's content changes, so clone it to make changes.");
            return snapshot;
        }

        return operation switch
        {
            DatasetSaveSettingsOperation save => DatasetSettingsEdits.SaveSettings(snapshot, save, context),
            DatasetRenameSourceTableOperation rename => DatasetSettingsEdits.RenameSourceTable(snapshot, rename, context),
            DatasetSaveFieldOperation save => DatasetFieldEdits.Save(snapshot, save, context),
            DatasetDeleteFieldOperation delete => DatasetFieldEdits.Delete(snapshot, delete, context),
            DatasetAddFieldsFromColumnsOperation add => DatasetFieldEdits.AddFromColumns(snapshot, add, context),
            DatasetSaveGrainOperation save => DatasetGrainEdits.Save(snapshot, save, context),
            DatasetDeleteGrainOperation delete => DatasetGrainEdits.Delete(snapshot, delete, context),
            DatasetSaveBindingOperation save => DatasetBindingEdits.Save(snapshot, save, context),
            DatasetDeleteBindingOperation delete => DatasetBindingEdits.Delete(snapshot, delete, context),
            DatasetSaveMeasureOperation save => DatasetMeasureEdits.Save(snapshot, save, context),
            DatasetDeleteMeasureOperation delete => DatasetMeasureEdits.Delete(snapshot, delete, context),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Not a dataset operation.")
        };
    }

    /// <summary>Plans the managed-table DDL, then the row writes, the version and dataset rows last.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <param name="plan">The plan.</param>
    public void Plan(DatasetSnapshot before, DatasetSnapshot after, WritePlan plan)
    {
        schema.Plan(before, after, plan);
        if (before.Dataset != after.Dataset) plan.Update(datasets, WritePlan.Root, after.Dataset);
        if (before.Version != after.Version) plan.Update(versions, WritePlan.Root, after.Version);
        plan.Sync(managed, ItemDepth, before.ManagedObjects, after.ManagedObjects, row => new { row.ManagedSourceObjectId });
        plan.Sync(fields, ItemDepth, before.Fields, after.Fields, row => new { row.FieldId });
        plan.Sync(grains, ItemDepth, before.Grains, after.Grains, row => new { row.DatasetGrainId });
        plan.Sync(measures, ItemDepth, before.Measures, after.Measures, row => new { row.MeasureId });
        plan.Sync(bindings, ItemReferenceDepth, before.Bindings, after.Bindings, row => new { row.DimensionBindingId });
        plan.Sync(grainFields, ItemReferenceDepth, before.GrainFields, after.GrainFields, row => new { row.DatasetGrainId, row.Ordinal });
        plan.Sync(components, ItemReferenceDepth, before.Components, after.Components, row => new { row.MeasureId, row.ComponentRole });
        plan.Sync(dependencies, ItemReferenceDepth, before.Dependencies, after.Dependencies, row => new { row.MeasureId, row.DependencyMeasureId });
        plan.Sync(timeBindings, BindingReferenceDepth, before.TimeBindings, after.TimeBindings, row => new { row.DimensionBindingId });
        plan.Sync(timePolicies, BindingReferenceDepth, before.TimePolicies, after.TimePolicies, row => new { row.MeasureId });
    }

    /// <summary>Whether the engine reads the change: only the dataset identity, since draft content reaches no query.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <returns>True when the commit advances the semantic revision.</returns>
    public bool IsEngineVisible(DatasetSnapshot before, DatasetSnapshot after)
    {
        return before.Dataset != after.Dataset;
    }

    /// <summary>The version content of a snapshot, its materialized fields with their view's status.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <param name="cancellationToken">Cancels the view status read.</param>
    /// <returns>The content.</returns>
    public Task<DatasetVersion> ReadAsync(DatasetSnapshot snapshot, Func<long, string> presentId, CancellationToken cancellationToken)
    {
        return reader.ReadAsync(snapshot, presentId, cancellationToken);
    }
}
