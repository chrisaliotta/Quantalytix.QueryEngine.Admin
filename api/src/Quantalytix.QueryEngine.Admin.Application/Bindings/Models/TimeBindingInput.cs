namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>The time extension of a <c>time</c> binding (schema section 12), stored in <c>qe_time_binding</c>.</summary>
public sealed record TimeBindingInput
{
    /// <summary>A <c>time_value_type</c> value: <c>date</c> or <c>timestamp</c>.</summary>
    public required string TimeValueType { get; init; }

    /// <summary>An <c>observation_kind</c> value: <c>instant</c>, <c>snapshot</c> or <c>interval</c>.</summary>
    public required string ObservationKind { get; init; }

    /// <summary>The IANA time zone timestamp values are recorded in, such as <c>America/Chicago</c>; null for dates.</summary>
    public string? SourceTimeZone { get; init; }

    /// <summary>A <c>periodicity</c> value, or null for irregular or instant observations.</summary>
    public string? NativePeriodicity { get; init; }

    /// <summary>The field holding each interval's exclusive end, where the native periodicity cannot derive it.</summary>
    public long? IntervalEndFieldId { get; init; }

    /// <summary>The <c>periodicity</c> values queries may group this time axis by, each named once.</summary>
    public required IReadOnlyList<string> AllowedPeriodicities { get; init; }

    /// <summary>The IANA time zone of queries that name none, usually <c>UTC</c>.</summary>
    public required string DefaultQueryTimeZone { get; init; }

    /// <summary>A <c>cumulative_origin</c> value: <c>output_range_start</c> or <c>fixed_start</c>.</summary>
    public required string CumulativeOrigin { get; init; }

    /// <summary>The UTC start of cumulative calculations; required for the <c>fixed_start</c> origin and null otherwise.</summary>
    public DateTime? CumulativeFixedStart { get; init; }
}
