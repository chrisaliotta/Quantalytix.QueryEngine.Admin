namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_time_binding</c> row: the one-to-one time extension of a dimension binding whose kind is <c>time</c>.</summary>
public sealed record TimeBindingRecord
{
    /// <summary>Primary key; also the extended dimension binding.</summary>
    public required long BindingId { get; init; }

    /// <summary>A <c>time_value_type</c> value.</summary>
    public required string TimeValueType { get; init; }

    /// <summary>An <c>observation_kind</c> value.</summary>
    public required string ObservationKind { get; init; }

    /// <summary>IANA time zone for timestamp sources; null for date-only values.</summary>
    public string? SourceTimeZone { get; init; }

    /// <summary>A <c>periodicity</c> value, or null for irregular or instant observations.</summary>
    public string? NativePeriodicity { get; init; }

    /// <summary>Exclusive interval-end field when intervals cannot be derived from the native periodicity.</summary>
    public long? IntervalEndFieldId { get; init; }

    /// <summary>JSON array (raw text) of the <c>periodicity</c> values the source supports.</summary>
    public required string AllowedPeriodicitiesJson { get; init; }

    /// <summary>Default request time zone, usually <c>UTC</c>.</summary>
    public required string DefaultQueryTimeZone { get; init; }

    /// <summary>A <c>cumulative_origin</c> value.</summary>
    public required string CumulativeOrigin { get; init; }

    /// <summary>Cumulative start (UTC) when the cumulative origin is <c>fixed_start</c>.</summary>
    public DateTime? CumulativeFixedStart { get; init; }
}
