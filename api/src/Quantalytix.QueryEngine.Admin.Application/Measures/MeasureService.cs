using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Lists, creates, replaces and deletes the measures of dataset versions with their components and time policies. Saving a
/// measure writes the measure, then its components and its time policy, then derives the version's formula dependencies.
/// </summary>
public sealed class MeasureService(
    ITableGateway<MeasureRecord> measures,
    ITableGateway<MeasureComponentRecord> components,
    ITableGateway<MeasureDependencyRecord> dependencies,
    ITableGateway<MeasureTimePolicyRecord> timePolicies,
    MeasureDependencyDeriver deriver,
    IIdGenerator ids)
{
    /// <summary>The component roles in vocabulary order, which components are listed in.</summary>
    private static readonly List<string> RoleOrder = [.. ComponentRoles.Definition.Values.Select(value => value.Value)];

    /// <summary>Lists the measures of a dataset version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The measures sorted by key, each with its components, formula dependencies and time policy.</returns>
    public async Task<IReadOnlyList<MeasureDetail>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await measures.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var measureFilter = new { MeasureId = rows.Select(row => row.MeasureId).ToArray() };
        var componentsByMeasure = (await components.ListAsync(measureFilter, cancellationToken)).ToLookup(component => component.MeasureId);
        var dependencyIds = (await dependencies.ListAsync(measureFilter, cancellationToken))
            .ToLookup(dependency => dependency.MeasureId, dependency => dependency.DependencyMeasureId);
        var policies = (await timePolicies.ListAsync(measureFilter, cancellationToken)).ToDictionary(policy => policy.MeasureId);
        return
        [
            .. rows
                .OrderBy(row => row.MeasureKey, StringComparer.OrdinalIgnoreCase)
                .Select(row => Detail(row, componentsByMeasure[row.MeasureId], dependencyIds[row.MeasureId], policies.GetValueOrDefault(row.MeasureId)))
        ];
    }

    /// <summary>Creates a measure in a draft dataset version with its components and time policy, then derives the formula dependencies.</summary>
    /// <param name="versionId">The draft version.</param>
    /// <param name="input">The new measure.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The new measure.</returns>
    public async Task<MeasureDetail> CreateAsync(long versionId, MeasureInput input, CancellationToken cancellationToken)
    {
        var measure = ToRecord(ids.NextId(), versionId, input.MeasureKey, input);
        await measures.InsertAsync(measure, cancellationToken);
        return await SaveChildrenAsync(measure, input, cancellationToken);
    }

    /// <summary>
    /// Replaces a measure's attributes, components and time policy, then derives the formula dependencies again; the key
    /// stays.
    /// </summary>
    /// <param name="reference">The measure and its draft version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The updated measure.</returns>
    public async Task<MeasureDetail> UpdateAsync(MeasureReference reference, MeasureUpdateInput input, CancellationToken cancellationToken)
    {
        var current = await measures.GetAsync(new { reference.MeasureId }, cancellationToken);
        var measure = ToRecord(current.MeasureId, current.DatasetVersionId, current.MeasureKey, input);
        await measures.UpdateAsync(measure, cancellationToken);
        await components.DeleteAsync(new { reference.MeasureId }, cancellationToken);
        await timePolicies.DeleteAsync(new { reference.MeasureId }, cancellationToken);
        return await SaveChildrenAsync(measure, input, cancellationToken);
    }

    /// <summary>
    /// Deletes a measure that no other formula depends on: its components, its own dependency rows and its time policy
    /// first, then the measure. Only the measure's own rows can still name it once no other formula depends on it.
    /// </summary>
    /// <param name="reference">The measure and its draft version.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the measure was deleted; otherwise the detail naming the formulas that depend on it.</returns>
    public async Task<string?> DeleteAsync(MeasureReference reference, CancellationToken cancellationToken)
    {
        var measure = await measures.GetAsync(new { reference.MeasureId }, cancellationToken);
        var dependents = (await dependencies.ListAsync(new { DependencyMeasureId = reference.MeasureId }, cancellationToken))
            .Where(dependency => dependency.MeasureId != reference.MeasureId)
            .Select(dependency => dependency.MeasureId)
            .ToArray();
        var formulas = await measures.ListAsync(new { MeasureId = dependents }, cancellationToken);

        var blockers = new DeleteBlockers($"Measure '{measure.MeasureKey}'");
        blockers.AddNamed("it is referenced by the formulas of measures", formulas.Select(formula => $"'{formula.MeasureKey}'"));
        if (blockers.Detail() is { } detail) return detail;

        await components.DeleteAsync(new { reference.MeasureId }, cancellationToken);
        await dependencies.DeleteAsync(new { reference.MeasureId }, cancellationToken);
        await timePolicies.DeleteAsync(new { reference.MeasureId }, cancellationToken);
        await measures.DeleteAsync(new { reference.MeasureId }, cancellationToken);
        return null;
    }

    /// <summary>Writes a saved measure's components and time policy, then derives the version's formula dependencies.</summary>
    private async Task<MeasureDetail> SaveChildrenAsync(MeasureRecord measure, MeasureUpdateInput input, CancellationToken cancellationToken)
    {
        MeasureComponentRecord[] componentRows =
        [
            .. input.Components.Select(component => new MeasureComponentRecord
            {
                MeasureId = measure.MeasureId,
                ComponentRole = component.ComponentRole,
                FieldId = component.FieldId!.Value
            })
        ];
        await components.InsertManyAsync(componentRows, cancellationToken);
        MeasureTimePolicyRecord? policy = null;
        if (input.TimePolicy is { } timePolicy)
        {
            policy = ToRecord(measure.MeasureId, timePolicy);
            await timePolicies.InsertAsync(policy, cancellationToken);
        }

        var derived = await deriver.DeriveAsync(measure.DatasetVersionId, cancellationToken);
        var dependencyIds = derived.Where(dependency => dependency.MeasureId == measure.MeasureId).Select(dependency => dependency.DependencyMeasureId);
        return Detail(measure, componentRows, dependencyIds, policy);
    }

    /// <summary>A measure with its components in role order, its dependencies by ID and its time policy.</summary>
    private static MeasureDetail Detail(
        MeasureRecord measure,
        IEnumerable<MeasureComponentRecord> componentRows,
        IEnumerable<long> dependencyIds,
        MeasureTimePolicyRecord? policy)
    {
        return new MeasureDetail
        {
            Measure = measure,
            Components = [.. componentRows.OrderBy(component => RoleOrder.IndexOf(component.ComponentRole))],
            DependencyMeasureIds = [.. dependencyIds.Order()],
            TimePolicy = policy
        };
    }

    /// <summary>A measure row holding the input's attributes.</summary>
    private static MeasureRecord ToRecord(long measureId, long versionId, string measureKey, MeasureUpdateInput input)
    {
        return new MeasureRecord
        {
            MeasureId = measureId,
            DatasetVersionId = versionId,
            MeasureKey = measureKey,
            DisplayName = input.DisplayName,
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
            AccumulatorPrecision = input.AccumulatorPrecision!.Value,
            AccumulatorScale = input.AccumulatorScale!.Value,
            RoundingMode = input.RoundingMode,
            UnitKey = input.UnitKey,
            ImplementationKey = input.ImplementationKey,
            ImplementationVersion = input.ImplementationVersion,
            ConfigurationJson = input.ConfigurationJson,
            IsEnabled = input.IsEnabled
        };
    }

    /// <summary>A time policy row holding the input's attributes.</summary>
    private static MeasureTimePolicyRecord ToRecord(long measureId, MeasureTimePolicyInput input)
    {
        return new MeasureTimePolicyRecord
        {
            MeasureId = measureId,
            RequiredTimeBindingId = input.RequiredTimeBindingId,
            SelectionDatasetGrainId = input.SelectionDatasetGrainId,
            ObservationSelection = input.ObservationSelection,
            ValueTimeRole = input.ValueTimeRole,
            BoundaryMatch = input.BoundaryMatch,
            MaxLookbackSeconds = input.MaxLookbackSeconds,
            DurationUnit = input.DurationUnit,
            PartialIntervalBehavior = input.PartialIntervalBehavior,
            MissingPeriodBehavior = input.MissingPeriodBehavior
        };
    }
}
