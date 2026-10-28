using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// The measure operations of the dataset change set: keys fixed and unique per version (D4), components and time policy naming
/// items of the version, and the formula dependencies derived again from every formula whenever measures change.
/// </summary>
internal static class DatasetMeasureEdits
{
    /// <summary><c>saveMeasure</c>: replaces the measure's components and time policy.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the measure saved.</returns>
    public static DatasetSnapshot Save(DatasetSnapshot snapshot, DatasetSaveMeasureOperation operation, ChangeContext context)
    {
        var input = operation.Input;
        var measureId = DatasetEditTargets.SavedId(input.Id, context);
        var existing = snapshot.Measures.SingleOrDefault(measure => measure.MeasureId == measureId);
        if (DatasetEditTargets.IsGone(input.Id, existing is not null, "Measure", context)) return snapshot;
        var others = snapshot.Measures.Where(measure => measure.MeasureId != measureId).Select(measure => measure.MeasureKey);
        if (DatasetEditTargets.IsKeyRefused(existing?.MeasureKey, input.Key, others, "measure", context)) return snapshot;

        var components = input.Components
            .Select(component => new MeasureComponentRecord { MeasureId = measureId, ComponentRole = component.Role, FieldId = context.Ids.Resolve(component.FieldId) })
            .ToList();
        foreach (var component in components.Where(component => snapshot.Fields.All(field => field.FieldId != component.FieldId)))
            context.Reject($"input.components.{component.ComponentRole}", "Choose a field of this version.");
        var policy = input.TimePolicy is { } timePolicy ? PolicyOf(measureId, timePolicy, context) : null;
        if (policy?.RequiredTimeBindingId is { } bindingId && snapshot.TimeBindings.All(time => time.DimensionBindingId != bindingId))
            context.Reject("input.timePolicy.requiredTimeBindingId", "Choose a time binding of this version.");
        if (policy?.SelectionDatasetGrainId is { } grainId && snapshot.Grains.All(grain => grain.DatasetGrainId != grainId))
            context.Reject("input.timePolicy.selectionDatasetGrainId", "Choose a grain of this version.");
        if (context.Refused) return snapshot;

        var saved = new MeasureRecord
        {
            MeasureId = measureId,
            DatasetVersionId = snapshot.VersionId,
            MeasureKey = input.Key,
            DisplayName = input.DisplayName.Trim(),
            MeasureKind = input.MeasureKind,
            AggregationFunction = input.AggregationFunction,
            ExpressionText = input.ExpressionText,
            RowFilterExpression = input.RowFilterExpression,
            ValueNullBehavior = input.ValueNullBehavior,
            NegativeWeightBehavior = input.NegativeWeightBehavior,
            EmptySetBehavior = input.EmptySetBehavior,
            BridgeFactorBehavior = input.BridgeFactorBehavior,
            BridgeAllocationBehavior = input.BridgeAllocationBehavior,
            TotalBehavior = input.TotalBehavior,
            OutputDataType = input.OutputDataType,
            OutputPrecision = input.OutputPrecision,
            OutputScale = input.OutputScale,
            AccumulatorPrecision = input.AccumulatorPrecision,
            AccumulatorScale = input.AccumulatorScale,
            RoundingMode = input.RoundingMode,
            UnitKey = string.IsNullOrWhiteSpace(input.UnitKey) ? null : input.UnitKey.Trim(),
            ImplementationKey = input.ImplementationKey,
            ImplementationVersion = input.ImplementationVersion,
            ConfigurationJson = input.ConfigurationJson,
            IsEnabled = input.IsEnabled
        };
        IReadOnlyList<MeasureTimePolicyRecord> policies = policy is null ? [] : [policy];
        return WithMeasures(
            snapshot with
            {
                Components = [.. snapshot.Components.Where(component => component.MeasureId != measureId), .. components],
                TimePolicies = [.. snapshot.TimePolicies.Where(row => row.MeasureId != measureId), .. policies]
            },
            [.. snapshot.Measures.Where(measure => measure.MeasureId != measureId), saved]);
    }

    /// <summary><c>deleteMeasure</c>: formulas name measures by key, so a formula that named it reports a finding instead.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate without the measure, its components and its time policy.</returns>
    public static DatasetSnapshot Delete(DatasetSnapshot snapshot, DatasetDeleteMeasureOperation operation, ChangeContext context)
    {
        if (DatasetEditTargets.Find(snapshot.Measures, measure => measure.MeasureId, operation.MeasureId, "Measure", context) is not { } measure) return snapshot;
        var measureId = measure.MeasureId;
        return WithMeasures(
            snapshot with
            {
                Components = [.. snapshot.Components.Where(component => component.MeasureId != measureId)],
                TimePolicies = [.. snapshot.TimePolicies.Where(policy => policy.MeasureId != measureId)]
            },
            [.. snapshot.Measures.Where(row => row.MeasureId != measureId)]);
    }

    /// <summary>The snapshot with new measures and the dependencies their formulas derive.</summary>
    private static DatasetSnapshot WithMeasures(DatasetSnapshot snapshot, IReadOnlyList<MeasureRecord> measures)
    {
        return snapshot with { Measures = measures, Dependencies = MeasureDependencies.Of(measures) };
    }

    /// <summary>The time policy row of a measure.</summary>
    private static MeasureTimePolicyRecord PolicyOf(long measureId, DatasetMeasureTimePolicy policy, ChangeContext context)
    {
        return new MeasureTimePolicyRecord
        {
            MeasureId = measureId,
            RequiredTimeBindingId = DatasetEditTargets.Optional(policy.RequiredTimeBindingId, context),
            SelectionDatasetGrainId = DatasetEditTargets.Optional(policy.SelectionDatasetGrainId, context),
            ObservationSelection = policy.ObservationSelection,
            ValueTimeRole = policy.ValueTimeRole,
            BoundaryMatch = policy.BoundaryMatch,
            MaxLookbackSeconds = policy.MaxLookbackSeconds,
            DurationUnit = policy.DurationUnit,
            PartialIntervalBehavior = policy.PartialIntervalBehavior,
            MissingPeriodBehavior = policy.MissingPeriodBehavior
        };
    }
}
