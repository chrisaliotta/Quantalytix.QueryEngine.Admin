using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// The shape of a time policy: known vocabulary values, a finite positive lookback for prior-observation matching, and a selection
/// grain for the selections that pick one observation per series.
/// </summary>
public sealed class DatasetMeasureTimePolicyValidator : AbstractValidator<DatasetMeasureTimePolicy>
{
    /// <summary>The boundary matches that look back for a prior observation.</summary>
    private static readonly string[] PriorMatches = [BoundaryMatches.OnOrBefore, BoundaryMatches.StrictlyBefore];

    /// <summary>The selections that pick one observation per series.</summary>
    private static readonly string[] SingleObservationSelections =
        [ObservationSelections.FirstInPeriod, ObservationSelections.LastInPeriod, ObservationSelections.AtStart, ObservationSelections.AtEnd];

    /// <summary>Declares the rules.</summary>
    public DatasetMeasureTimePolicyValidator()
    {
        RuleFor(policy => policy.RequiredTimeBindingId).MustBeChangeSetId();
        RuleFor(policy => policy.SelectionDatasetGrainId).MustBeChangeSetId();
        RuleFor(policy => policy.ObservationSelection).MustBeIn(ObservationSelections.Definition);
        RuleFor(policy => policy.ValueTimeRole).MustBeIn(ValueTimeRoles.Definition);
        RuleFor(policy => policy.BoundaryMatch).MustBeIn(BoundaryMatches.Definition);
        RuleFor(policy => policy.DurationUnit).MustBeIn(DurationUnits.Definition);
        RuleFor(policy => policy.PartialIntervalBehavior).MustBeIn(PartialIntervalBehaviors.Definition);
        RuleFor(policy => policy.MissingPeriodBehavior).MustBeIn(MissingPeriodBehaviors.Definition);
        RuleFor(policy => policy.MaxLookbackSeconds).GreaterThan(0);
        RuleFor(policy => policy.MaxLookbackSeconds)
            .NotNull()
            .WithMessage(policy => $"'{{PropertyName}}' is required for {policy.BoundaryMatch} matching, because the lookback for a prior observation must be finite.")
            .When(policy => PriorMatches.Contains(policy.BoundaryMatch));
        RuleFor(policy => policy.SelectionDatasetGrainId)
            .NotNull()
            .WithMessage(policy => $"'{{PropertyName}}' is required for the {policy.ObservationSelection} selection, which picks one observation per series of that grain.")
            .When(policy => SingleObservationSelections.Contains(policy.ObservationSelection));
    }
}
