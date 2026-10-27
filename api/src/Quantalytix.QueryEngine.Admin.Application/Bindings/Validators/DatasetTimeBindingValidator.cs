using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// The shape of a time axis: known vocabulary values, IANA time zones, a source zone exactly for timestamps, each periodicity once,
/// and a fixed start exactly for the <c>fixed_start</c> origin.
/// </summary>
public sealed class DatasetTimeBindingValidator : AbstractValidator<DatasetTimeBinding>
{
    /// <summary>The message of a value that is not an IANA zone.</summary>
    private const string IanaTimeZoneMessage = "'{PropertyName}' must be an IANA time zone, such as 'UTC' or 'America/Chicago'.";

    /// <summary>Declares the rules.</summary>
    public DatasetTimeBindingValidator()
    {
        RuleFor(time => time.TimeValueType).MustBeIn(TimeValueTypes.Definition);
        RuleFor(time => time.ObservationKind).MustBeIn(ObservationKinds.Definition);
        RuleFor(time => time.NativePeriodicity).MustBeIn(Periodicities.Definition);
        RuleFor(time => time.CumulativeOrigin).MustBeIn(CumulativeOrigins.Definition);
        RuleFor(time => time.IntervalEndFieldId).MustBeChangeSetId();
        RuleFor(time => time.AllowedPeriodicities)
            .NotEmpty()
            .Must(periodicities => periodicities.Distinct(StringComparer.Ordinal).Count() == periodicities.Count)
            .WithMessage("'{PropertyName}' must name each periodicity once.");
        RuleForEach(time => time.AllowedPeriodicities).MustBeIn(Periodicities.Definition);
        RuleFor(time => time.DefaultQueryTimeZone).Cascade(CascadeMode.Stop).NotEmpty().Must(BeIanaTimeZone).WithMessage(IanaTimeZoneMessage);

        When(time => time.TimeValueType == TimeValueTypes.Timestamp, () =>
        {
            RuleFor(time => time.SourceTimeZone)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("'{PropertyName}' is required for timestamp values: it names the zone their wall-clock times are recorded in.")
                .Must(BeIanaTimeZone)
                .WithMessage(IanaTimeZoneMessage);
        });
        When(time => time.TimeValueType == TimeValueTypes.Date, () =>
        {
            RuleFor(time => time.SourceTimeZone).Null().WithMessage("Date values carry no time of day, so '{PropertyName}' must be empty.");
        });

        When(time => time.CumulativeOrigin == CumulativeOrigins.FixedStart, () =>
        {
            RuleFor(time => time.CumulativeFixedStart).NotNull().WithMessage("'{PropertyName}' is required for the fixed_start cumulative origin.");
        }).Otherwise(() =>
        {
            RuleFor(time => time.CumulativeFixedStart).Null().WithMessage("Only the fixed_start cumulative origin uses '{PropertyName}', so it must be empty.");
        });
    }

    /// <summary>Whether text names an IANA time zone.</summary>
    private static bool BeIanaTimeZone(string? timeZone)
    {
        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZone!, out var zone) && zone.HasIanaId;
    }
}
