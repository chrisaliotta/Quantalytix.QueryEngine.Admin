namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_measure_time_policy</c> row: the optional temporal observation-selection and duration policy of a measure.</summary>
public sealed record MeasureTimePolicyRecord
{
    /// <summary>Primary key; also the owning measure.</summary>
    public required long MeasureId { get; init; }

    /// <summary>Time binding the measure requires; null uses the query or default binding.</summary>
    public long? RequiredTimeBindingId { get; init; }

    /// <summary>The <c>series</c> grain selection runs over before dimensional aggregation.</summary>
    public long? SelectionGrainId { get; init; }

    /// <summary>An <c>observation_selection</c> value.</summary>
    public required string ObservationSelection { get; init; }

    /// <summary>A <c>value_time_role</c> value.</summary>
    public required string ValueTimeRole { get; init; }

    /// <summary>A <c>boundary_match</c> value.</summary>
    public required string BoundaryMatch { get; init; }

    /// <summary>Maximum prior-observation search distance, in seconds, for non-exact boundary matching.</summary>
    public long? MaxLookbackSeconds { get; init; }

    /// <summary>A <c>duration_unit</c> value.</summary>
    public required string DurationUnit { get; init; }

    /// <summary>A <c>partial_interval_behavior</c> value.</summary>
    public required string PartialIntervalBehavior { get; init; }

    /// <summary>A <c>missing_period_behavior</c> value.</summary>
    public required string MissingPeriodBehavior { get; init; }
}
