using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Checks the time extension of a time binding against schema section 12: vocabulary values, a non-empty set of distinct
/// allowed periodicities, a source time zone exactly for timestamp values, time zones the query engine accepts, and a UTC
/// fixed start exactly for the <c>fixed_start</c> cumulative origin.
/// </summary>
public sealed class TimeBindingInputValidator : AbstractValidator<TimeBindingInput>
{
    /// <summary>The message of a time zone the query engine would not accept.</summary>
    private const string IanaTimeZoneMessage = "'{PropertyName}' must be an IANA time zone, such as 'UTC' or 'America/Chicago'.";

    /// <summary>Declares the rules.</summary>
    public TimeBindingInputValidator()
    {
        RuleFor(input => input.TimeValueType).MustBeIn(TimeValueTypes.Definition);
        RuleFor(input => input.ObservationKind).MustBeIn(ObservationKinds.Definition);
        RuleFor(input => input.NativePeriodicity).MustBeIn(Periodicities.Definition);
        RuleFor(input => input.CumulativeOrigin).MustBeIn(CumulativeOrigins.Definition);
        RuleFor(input => input.AllowedPeriodicities)
            .NotEmpty()
            .Must(periodicities => periodicities.Distinct(StringComparer.Ordinal).Count() == periodicities.Count)
            .WithMessage("'{PropertyName}' must name each periodicity once.");
        RuleForEach(input => input.AllowedPeriodicities).MustBeIn(Periodicities.Definition);
        RuleFor(input => input.DefaultQueryTimeZone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(BeIanaTimeZone)
            .WithMessage(IanaTimeZoneMessage);

        When(input => input.TimeValueType == TimeValueTypes.Timestamp, () =>
        {
            RuleFor(input => input.SourceTimeZone)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("'{PropertyName}' is required for timestamp values: it names the zone their wall-clock times are recorded in.")
                .Must(BeIanaTimeZone)
                .WithMessage(IanaTimeZoneMessage);
        });
        When(input => input.TimeValueType == TimeValueTypes.Date, () =>
        {
            RuleFor(input => input.SourceTimeZone).Null().WithMessage("Date values carry no time of day, so '{PropertyName}' must be empty.");
        });

        When(input => input.CumulativeOrigin == CumulativeOrigins.FixedStart, () =>
        {
            RuleFor(input => input.CumulativeFixedStart)
                .NotNull()
                .WithMessage("'{PropertyName}' is required for the fixed_start cumulative origin.")
                .MustBeUtc();
        }).Otherwise(() =>
        {
            RuleFor(input => input.CumulativeFixedStart)
                .Null()
                .WithMessage("Only the fixed_start cumulative origin uses '{PropertyName}', so it must be empty.");
        });
    }

    /// <summary>
    /// Whether a time zone is one the query engine accepts: a zone the system knows by its IANA name, the engine's own test.
    /// Windows names such as <c>Eastern Standard Time</c> resolve on most systems but carry no IANA name, so they fail.
    /// </summary>
    private static bool BeIanaTimeZone(string? timeZone)
    {
        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZone!, out var zone) && zone.HasIanaId;
    }
}
