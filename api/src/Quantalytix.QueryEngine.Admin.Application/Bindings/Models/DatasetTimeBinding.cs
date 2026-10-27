namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>The contract's <c>DatasetTimeBinding</c>: the time axis of a <c>time</c> binding (<c>qe_time_binding</c>).</summary>
public sealed record DatasetTimeBinding
{
    /// <summary>A <c>time_value_type</c> value.</summary>
    public required string TimeValueType { get; init; }

    /// <summary>The IANA zone of timestamp values; null for dates.</summary>
    public required string? SourceTimeZone { get; init; }

    /// <summary>The IANA zone requests default to.</summary>
    public required string DefaultQueryTimeZone { get; init; }

    /// <summary>An <c>observation_kind</c> value.</summary>
    public required string ObservationKind { get; init; }

    /// <summary>A <c>periodicity</c> value, or null for irregular or instant observations.</summary>
    public required string? NativePeriodicity { get; init; }

    /// <summary>The exclusive interval-end field, stored or temporary.</summary>
    public required string? IntervalEndFieldId { get; init; }

    /// <summary>The <c>periodicity</c> values requests may read the binding at.</summary>
    public required IReadOnlyList<string> AllowedPeriodicities { get; init; }

    /// <summary>A <c>cumulative_origin</c> value.</summary>
    public required string CumulativeOrigin { get; init; }

    /// <summary>The cumulative start of the <c>fixed_start</c> origin.</summary>
    public required DateOnly? CumulativeFixedStart { get; init; }
}
