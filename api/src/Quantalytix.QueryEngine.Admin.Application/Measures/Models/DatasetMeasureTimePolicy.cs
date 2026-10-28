namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>The contract's <c>DatasetMeasureTimePolicy</c>: a measure's observation selection and duration policy (<c>qe_measure_time_policy</c>).</summary>
public sealed record DatasetMeasureTimePolicy
{
    /// <summary>The time binding the measure requires, stored or temporary; null uses the query's or the default.</summary>
    public required string? RequiredTimeBindingId { get; init; }

    /// <summary>The series grain selection runs over, stored or temporary.</summary>
    public required string? SelectionDatasetGrainId { get; init; }

    /// <summary>An <c>observation_selection</c> value.</summary>
    public required string ObservationSelection { get; init; }

    /// <summary>A <c>value_time_role</c> value.</summary>
    public required string ValueTimeRole { get; init; }

    /// <summary>A <c>boundary_match</c> value.</summary>
    public required string BoundaryMatch { get; init; }

    /// <summary>How far back a non-exact match looks, in seconds.</summary>
    public required long? MaxLookbackSeconds { get; init; }

    /// <summary>A <c>duration_unit</c> value.</summary>
    public required string DurationUnit { get; init; }

    /// <summary>A <c>partial_interval_behavior</c> value.</summary>
    public required string PartialIntervalBehavior { get; init; }

    /// <summary>A <c>missing_period_behavior</c> value.</summary>
    public required string MissingPeriodBehavior { get; init; }
}
