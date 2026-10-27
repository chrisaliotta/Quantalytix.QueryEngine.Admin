using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>Builds the dataset rows the dataset tests seed, with the schema defaults for every attribute a test does not care about.</summary>
internal static class DatasetRows
{
    /// <summary>A draft version of dataset 10 over the table <c>loan_daily</c>.</summary>
    public static DatasetVersionRecord Version(long versionId, long? defaultTimeBindingId = null)
    {
        return new DatasetVersionRecord
        {
            CreatedAt = DateTime.UnixEpoch,
            DatasetVersionId = versionId,
            DatasetId = 10,
            VersionNumber = 1,
            Status = PublicationStatuses.Draft,
            SourceObjectKind = SourceObjectKinds.Table,
            SourceObject = "loan_daily",
            SupportsAggregate = true,
            SupportsDetail = true,
            DefaultTimeBindingId = defaultTimeBindingId,
            ExpressionLanguageVersion = "1.0"
        };
    }

    /// <summary>A <c>column</c> field of a version.</summary>
    public static FieldRecord ColumnField(long fieldId, long versionId, string fieldKey, string sourceColumn, string dataType = DataTypes.String)
    {
        return Field(fieldId, versionId, fieldKey, dataType) with { FieldSourceKind = FieldSourceKinds.Column, SourceColumn = sourceColumn };
    }

    /// <summary>An <c>expression</c> field of a version.</summary>
    public static FieldRecord ExpressionField(long fieldId, long versionId, string fieldKey, string expressionText)
    {
        return Field(fieldId, versionId, fieldKey, DataTypes.Decimal) with
        {
            FieldSourceKind = FieldSourceKinds.Expression,
            ExpressionText = expressionText,
            NumericPrecision = 20,
            NumericScale = 2
        };
    }

    /// <summary>A grain of a version.</summary>
    public static DatasetGrainRecord Grain(long datasetGrainId, long versionId, string grainKey, string grainKind)
    {
        return new DatasetGrainRecord { DatasetGrainId = datasetGrainId, DatasetVersionId = versionId, GrainKey = grainKey, GrainKind = grainKind };
    }

    /// <summary>A component field of a grain.</summary>
    public static DatasetGrainFieldRecord GrainField(long datasetGrainId, int ordinal, long fieldId)
    {
        return new DatasetGrainFieldRecord { DatasetGrainId = datasetGrainId, Ordinal = ordinal, FieldId = fieldId };
    }

    /// <summary>A binding of a version to dimension 7000: of a role of it, or with no role for a time binding.</summary>
    public static DimensionBindingRecord Binding(long dimensionBindingId, long versionId, long? dimensionRoleId, string bindingKind, long? sourceFieldId)
    {
        return new DimensionBindingRecord
        {
            DimensionBindingId = dimensionBindingId,
            DatasetVersionId = versionId,
            DimensionRoleId = dimensionRoleId,
            DimensionId = 7000,
            BindingKind = bindingKind,
            SourceFieldId = sourceFieldId,
            RaggedBehavior = RaggedBehaviors.Null,
            DefaultPeriodAlignment = PeriodAlignments.Observation,
            UnmappedBehavior = UnmappedBehaviors.Null,
            IsRequired = false,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true
        };
    }

    /// <summary>The time extension of a binding: daily snapshots of a date.</summary>
    public static TimeBindingRecord TimeBinding(long dimensionBindingId, long? intervalEndFieldId = null)
    {
        return new TimeBindingRecord
        {
            DimensionBindingId = dimensionBindingId,
            TimeValueType = TimeValueTypes.Date,
            ObservationKind = ObservationKinds.Snapshot,
            NativePeriodicity = Periodicities.Day,
            IntervalEndFieldId = intervalEndFieldId,
            AllowedPeriodicitiesJson = """["day","month"]""",
            DefaultQueryTimeZone = "UTC",
            CumulativeOrigin = CumulativeOrigins.OutputRangeStart
        };
    }

    /// <summary>A measure of a version with the schema's default behaviors.</summary>
    public static MeasureRecord Measure(long measureId, long versionId, string measureKey, string measureKind)
    {
        return new MeasureRecord
        {
            MeasureId = measureId,
            DatasetVersionId = versionId,
            MeasureKey = measureKey,
            DisplayName = measureKey,
            MeasureKind = measureKind,
            ValueNullBehavior = ValueNullBehaviors.Exclude,
            NegativeWeightBehavior = NegativeWeightBehaviors.Allow,
            EmptySetBehavior = EmptySetBehaviors.Null,
            BridgeFactorBehavior = BridgeFactorBehaviors.Ignore,
            BridgeAllocationBehavior = BridgeAllocationBehaviors.Apply,
            TotalBehavior = TotalBehaviors.Recompute,
            OutputDataType = DataTypes.Decimal,
            AccumulatorPrecision = 38,
            AccumulatorScale = 12,
            IsEnabled = true
        };
    }

    /// <summary>A time policy that selects each series' observation at the period end.</summary>
    public static MeasureTimePolicyRecord TimePolicy(long measureId, long? requiredTimeBindingId, long? selectionDatasetGrainId)
    {
        return new MeasureTimePolicyRecord
        {
            MeasureId = measureId,
            RequiredTimeBindingId = requiredTimeBindingId,
            SelectionDatasetGrainId = selectionDatasetGrainId,
            ObservationSelection = ObservationSelections.AtEnd,
            ValueTimeRole = ValueTimeRoles.Point,
            BoundaryMatch = BoundaryMatches.OnOrBefore,
            MaxLookbackSeconds = 86400,
            DurationUnit = DurationUnits.CalendarDay,
            PartialIntervalBehavior = PartialIntervalBehaviors.ExactOnly,
            MissingPeriodBehavior = MissingPeriodBehaviors.Null
        };
    }

    /// <summary>A field with the schema's default flags; callers set its source.</summary>
    private static FieldRecord Field(long fieldId, long versionId, string fieldKey, string dataType)
    {
        return new FieldRecord
        {
            FieldId = fieldId,
            DatasetVersionId = versionId,
            FieldKey = fieldKey,
            DisplayName = fieldKey,
            FieldSourceKind = FieldSourceKinds.Column,
            DataType = dataType,
            IsNullable = true,
            IsSelectable = true,
            IsFilterable = true,
            IsSortable = true,
            IsGroupable = false,
            IsEnabled = true
        };
    }
}
