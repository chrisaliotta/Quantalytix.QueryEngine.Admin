using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Builds the contract's dataset read models from rows. Every id of a version's content goes through <c>presentId</c>, so a preview
/// shows the items its change set created by their temporary ids and every other read shows stored ids.
/// </summary>
public static class DatasetReadModels
{
    /// <summary>The identity of a dataset row.</summary>
    /// <param name="dataset">The row.</param>
    /// <returns>The identity.</returns>
    public static Dataset Identity(DatasetRecord dataset)
    {
        return new Dataset
        {
            Id = TemporaryIds.PresentStored(dataset.DatasetId),
            Key = dataset.DatasetKey,
            DisplayName = dataset.DisplayName,
            IsEnabled = dataset.IsEnabled
        };
    }

    /// <summary>A list item.</summary>
    /// <param name="dataset">The dataset.</param>
    /// <param name="versions">Its versions.</param>
    /// <param name="releases">The release pins.</param>
    /// <param name="statistics">The source objects' statistics, by name.</param>
    /// <returns>The list item, versions newest first.</returns>
    public static DatasetListItem ListItem(
        DatasetRecord dataset,
        IEnumerable<DatasetVersionRecord> versions,
        DatasetReleaseIndex releases,
        IReadOnlyDictionary<string, DatasetSourceObjectStats> statistics)
    {
        var newestFirst = versions.OrderByDescending(version => version.VersionNumber).ToList();
        var active = releases.ActiveVersionOf(dataset.DatasetId);
        return new DatasetListItem(
            Identity(dataset),
            [.. newestFirst.Select(version => Summary(version, releases))],
            active is { } activeId ? TemporaryIds.PresentStored(activeId) : null,
            newestFirst.Count == 0 ? null : statistics.GetValueOrDefault(newestFirst[0].SourceObject));
    }

    /// <summary>A version's summary.</summary>
    /// <param name="version">The version.</param>
    /// <param name="releases">The release pins.</param>
    /// <returns>The summary.</returns>
    public static DatasetVersionSummary Summary(DatasetVersionRecord version, DatasetReleaseIndex releases)
    {
        return new DatasetVersionSummary
        {
            Id = TemporaryIds.PresentStored(version.DatasetVersionId),
            DatasetId = TemporaryIds.PresentStored(version.DatasetId),
            VersionNumber = version.VersionNumber,
            Status = version.Status,
            SourceObject = version.SourceObject,
            CreatedAt = version.CreatedAt,
            PublishedAt = version.PublishedAt,
            ClonedFromVersionId = version.ClonedFromDatasetVersionId is { } source ? TemporaryIds.PresentStored(source) : null,
            Releases = releases.PinsOf(version.DatasetVersionId)
        };
    }

    /// <summary>The whole content of a snapshot's version.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="views">The status of each materialized view, by name.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>The content.</returns>
    public static DatasetVersion Version(DatasetSnapshot snapshot, IReadOnlyDictionary<string, MaterializedViewStatus> views, Func<long, string> presentId)
    {
        var names = DatasetBindingNames.Of(snapshot);
        var timeBindings = snapshot.TimeBindings.ToDictionary(time => time.DimensionBindingId);
        var componentsOf = snapshot.Components.ToLookup(component => component.MeasureId);
        var policies = snapshot.TimePolicies.ToDictionary(policy => policy.MeasureId);
        var view = views.GetValueOrDefault(MaterializedViewNames.ViewOf(snapshot.Version.SourceObject));
        return new DatasetVersion(
            Summary(snapshot.Version, snapshot.Surroundings.Releases),
            Settings(snapshot.Version, presentId),
            [.. snapshot.Fields.OrderBy(field => field.FieldKey, StringComparer.OrdinalIgnoreCase).Select(field => Field(field, snapshot.Version.SourceObject, view, presentId))],
            [.. snapshot.Grains.OrderBy(grain => grain.GrainKey, StringComparer.OrdinalIgnoreCase).Select(grain => Grain(grain, snapshot.GrainFields, presentId))],
            [
                .. snapshot.Bindings
                    .OrderBy(binding => names[binding.DimensionBindingId], StringComparer.OrdinalIgnoreCase)
                    .Select(binding => Binding(binding, timeBindings.GetValueOrDefault(binding.DimensionBindingId), presentId))
            ],
            [
                .. snapshot.Measures
                    .OrderBy(measure => measure.MeasureKey, StringComparer.OrdinalIgnoreCase)
                    .Select(measure => Measure(measure, componentsOf[measure.MeasureId], policies.GetValueOrDefault(measure.MeasureId), presentId))
            ],
            DatasetDependents.Of(snapshot, names, presentId));
    }

    /// <summary>The settings of a version row.</summary>
    /// <param name="version">The version.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>The settings.</returns>
    public static DatasetVersionSettings Settings(DatasetVersionRecord version, Func<long, string> presentId)
    {
        return new DatasetVersionSettings
        {
            SourceObjectKind = version.SourceObjectKind,
            SourceObject = version.SourceObject,
            SupportsAggregate = version.SupportsAggregate,
            SupportsDetail = version.SupportsDetail,
            DefaultTimeBindingId = version.DefaultTimeBindingId is { } id ? presentId(id) : null,
            ExpressionLanguageVersion = version.ExpressionLanguageVersion,
            SecurityPolicyKey = version.SecurityPolicyKey,
            Properties = version.Properties ?? "{}"
        };
    }

    /// <summary>
    /// A field. Its materialization is the view column it names with the view's status; a column of a view that does not exist is
    /// none, so the field reads its JSON path and can be materialized again.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="sourceObject">Its version's source object.</param>
    /// <param name="view">The status of the source object's materialized view, or null when there is none.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>The field.</returns>
    public static DatasetField Field(FieldRecord field, string sourceObject, MaterializedViewStatus? view, Func<long, string> presentId)
    {
        return new DatasetField
        {
            Id = presentId(field.FieldId),
            Key = field.FieldKey,
            DisplayName = field.DisplayName,
            FieldSourceKind = field.FieldSourceKind,
            SourceColumn = field.SourceColumn,
            JsonPath = field.JsonPath,
            ExpressionText = field.ExpressionText,
            DataType = field.DataType,
            NumericPrecision = field.NumericPrecision,
            NumericScale = field.NumericScale,
            IsNullable = field.IsNullable,
            UnitKey = field.UnitKey,
            IsSelectable = field.IsSelectable,
            IsFilterable = field.IsFilterable,
            IsSortable = field.IsSortable,
            IsGroupable = field.IsGroupable,
            IsEnabled = field.IsEnabled,
            Materialization = field.MaterializedColumn is { } column && view is not null
                ? new DatasetFieldMaterialization
                {
                    ViewName = MaterializedViewNames.ViewOf(sourceObject),
                    ColumnName = column,
                    State = view.State,
                    RequestedAt = view.DefinedAt,
                    RefreshedAt = view.RefreshedAt,
                    Error = view.Error
                }
                : null
        };
    }

    /// <summary>A grain with its components in ordinal order.</summary>
    private static DatasetGrain Grain(DatasetGrainRecord grain, IReadOnlyList<DatasetGrainFieldRecord> components, Func<long, string> presentId)
    {
        return new DatasetGrain
        {
            Id = presentId(grain.DatasetGrainId),
            Key = grain.GrainKey,
            GrainKind = grain.GrainKind,
            FieldIds = [.. components.Where(component => component.DatasetGrainId == grain.DatasetGrainId).OrderBy(component => component.Ordinal).Select(component => presentId(component.FieldId))]
        };
    }

    /// <summary>A binding with its time axis.</summary>
    private static DatasetBinding Binding(DimensionBindingRecord binding, TimeBindingRecord? time, Func<long, string> presentId)
    {
        string? Present(long? id) => id is { } value ? presentId(value) : null;
        return new DatasetBinding
        {
            Id = presentId(binding.DimensionBindingId),
            BindingKind = binding.BindingKind,
            DimensionRoleId = Present(binding.DimensionRoleId),
            DimensionId = binding.DimensionRoleId is null ? presentId(binding.DimensionId) : null,
            SourceFieldId = Present(binding.SourceFieldId),
            MappingSetId = Present(binding.MappingSetId),
            BridgeId = Present(binding.BridgeId),
            SourceDimensionBindingId = Present(binding.SourceDimensionBindingId),
            RaggedBehavior = binding.RaggedBehavior,
            DefaultPeriodAlignment = binding.DefaultPeriodAlignment,
            UnmappedBehavior = binding.UnmappedBehavior,
            UnknownMemberKey = binding.UnknownMemberKey,
            IsRequired = binding.IsRequired,
            IsGroupable = binding.IsGroupable,
            IsFilterable = binding.IsFilterable,
            IsEnabled = binding.IsEnabled,
            Time = time is null
                ? null
                : new DatasetTimeBinding
                {
                    TimeValueType = time.TimeValueType,
                    SourceTimeZone = time.SourceTimeZone,
                    DefaultQueryTimeZone = time.DefaultQueryTimeZone,
                    ObservationKind = time.ObservationKind,
                    NativePeriodicity = time.NativePeriodicity,
                    IntervalEndFieldId = Present(time.IntervalEndFieldId),
                    AllowedPeriodicities = JsonSerializer.Deserialize<string[]>(time.AllowedPeriodicitiesJson)!,
                    CumulativeOrigin = time.CumulativeOrigin,
                    CumulativeFixedStart = time.CumulativeFixedStart is { } start ? DateOnly.FromDateTime(start) : null
                }
        };
    }

    /// <summary>A measure with its components in role order and its time policy.</summary>
    private static DatasetMeasure Measure(MeasureRecord measure, IEnumerable<MeasureComponentRecord> components, MeasureTimePolicyRecord? policy, Func<long, string> presentId)
    {
        return new DatasetMeasure
        {
            Id = presentId(measure.MeasureId),
            Key = measure.MeasureKey,
            DisplayName = measure.DisplayName,
            IsEnabled = measure.IsEnabled,
            MeasureKind = measure.MeasureKind,
            AggregationFunction = measure.AggregationFunction,
            ExpressionText = measure.ExpressionText,
            ImplementationKey = measure.ImplementationKey,
            ImplementationVersion = measure.ImplementationVersion,
            ConfigurationJson = measure.ConfigurationJson,
            Components =
            [
                .. components
                    .OrderBy(component => MeasureComponentRoles.Order(component.ComponentRole))
                    .Select(component => new DatasetMeasureComponent { Role = component.ComponentRole, FieldId = presentId(component.FieldId) })
            ],
            RowFilterExpression = measure.RowFilterExpression,
            ValueNullBehavior = measure.ValueNullBehavior,
            NegativeWeightBehavior = measure.NegativeWeightBehavior,
            EmptySetBehavior = measure.EmptySetBehavior,
            BridgeFactorBehavior = measure.BridgeFactorBehavior,
            BridgeAllocationBehavior = measure.BridgeAllocationBehavior,
            TotalBehavior = measure.TotalBehavior,
            OutputDataType = measure.OutputDataType,
            OutputPrecision = measure.OutputPrecision,
            OutputScale = measure.OutputScale,
            RoundingMode = measure.RoundingMode,
            UnitKey = measure.UnitKey,
            AccumulatorPrecision = measure.AccumulatorPrecision,
            AccumulatorScale = measure.AccumulatorScale,
            TimePolicy = policy is null
                ? null
                : new DatasetMeasureTimePolicy
                {
                    RequiredTimeBindingId = policy.RequiredTimeBindingId is { } binding ? presentId(binding) : null,
                    SelectionDatasetGrainId = policy.SelectionDatasetGrainId is { } grain ? presentId(grain) : null,
                    ObservationSelection = policy.ObservationSelection,
                    ValueTimeRole = policy.ValueTimeRole,
                    BoundaryMatch = policy.BoundaryMatch,
                    MaxLookbackSeconds = policy.MaxLookbackSeconds,
                    DurationUnit = policy.DurationUnit,
                    PartialIntervalBehavior = policy.PartialIntervalBehavior,
                    MissingPeriodBehavior = policy.MissingPeriodBehavior
                }
        };
    }
}
