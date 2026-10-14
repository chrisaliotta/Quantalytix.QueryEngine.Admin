using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Checks a measure's time policy against schema sections 14 and 18: vocabulary values, a finite positive lookback for
/// matching a prior observation, and a series grain for every selection of single observations, which the query engine
/// selects per series. Whether the grain and the time binding belong to the version is checked by the publication rules.
/// </summary>
public sealed class MeasureTimePolicyInputValidator : AbstractValidator<MeasureTimePolicyInput>
{
    /// <summary>The boundary matches that search back for a prior observation, so their lookback must be bounded.</summary>
    private static readonly string[] PriorMatches = [BoundaryMatches.OnOrBefore, BoundaryMatches.StrictlyBefore];

    /// <summary>The observation selections that pick one observation per series and period.</summary>
    private static readonly string[] SingleObservationSelections =
        [ObservationSelections.FirstInPeriod, ObservationSelections.LastInPeriod, ObservationSelections.AtStart, ObservationSelections.AtEnd];

    /// <summary>Declares the rules.</summary>
    public MeasureTimePolicyInputValidator()
    {
        RuleFor(input => input.ObservationSelection).MustBeIn(ObservationSelections.Definition);
        RuleFor(input => input.ValueTimeRole).MustBeIn(ValueTimeRoles.Definition);
        RuleFor(input => input.BoundaryMatch).MustBeIn(BoundaryMatches.Definition);
        RuleFor(input => input.DurationUnit).MustBeIn(DurationUnits.Definition);
        RuleFor(input => input.PartialIntervalBehavior).MustBeIn(PartialIntervalBehaviors.Definition);
        RuleFor(input => input.MissingPeriodBehavior).MustBeIn(MissingPeriodBehaviors.Definition);
        RuleFor(input => input.MaxLookbackSeconds).GreaterThan(0);
        RuleFor(input => input.MaxLookbackSeconds)
            .NotNull()
            .WithMessage(input => $"'{{PropertyName}}' is required for {input.BoundaryMatch} matching, because the lookback for a prior observation must be finite.")
            .When(input => PriorMatches.Contains(input.BoundaryMatch));
        RuleFor(input => input.SelectionGrainId)
            .NotNull()
            .WithMessage(input => $"'{{PropertyName}}' is required for the {input.ObservationSelection} selection, which picks one observation per series of that grain.")
            .When(input => SingleObservationSelections.Contains(input.ObservationSelection));
    }
}
