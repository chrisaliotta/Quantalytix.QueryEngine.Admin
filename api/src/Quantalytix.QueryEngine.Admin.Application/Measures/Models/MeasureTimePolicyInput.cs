namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>The temporal observation-selection and duration policy of a measure (schema section 14).</summary>
public sealed record MeasureTimePolicyInput
{
    /// <summary>The time binding the measure requires; null uses the query's or the dataset's default time binding.</summary>
    public long? RequiredTimeBindingId { get; init; }

    /// <summary>The <c>series</c> grain observations are selected per; required when the observation selection is not <c>all</c>.</summary>
    public long? SelectionGrainId { get; init; }

    /// <summary>An <c>observation_selection</c> value.</summary>
    public required string ObservationSelection { get; init; }

    /// <summary>A <c>value_time_role</c> value.</summary>
    public required string ValueTimeRole { get; init; }

    /// <summary>A <c>boundary_match</c> value.</summary>
    public required string BoundaryMatch { get; init; }

    /// <summary>
    /// How far back, in seconds, a boundary match may look for a prior observation; required for <c>on_or_before</c> and
    /// <c>strictly_before</c> matching, because the lookback must be finite.
    /// </summary>
    public long? MaxLookbackSeconds { get; init; }

    /// <summary>A <c>duration_unit</c> value.</summary>
    public required string DurationUnit { get; init; }

    /// <summary>A <c>partial_interval_behavior</c> value.</summary>
    public required string PartialIntervalBehavior { get; init; }

    /// <summary>A <c>missing_period_behavior</c> value.</summary>
    public required string MissingPeriodBehavior { get; init; }
}
