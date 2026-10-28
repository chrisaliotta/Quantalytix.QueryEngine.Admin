using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// The shape of a measure input (D7): known vocabulary values, the component roles its kind reads, an aggregation function only
/// where the kind takes one, a formula over measures only for formula measures, an implementation only for specialized measures,
/// output precision only for decimal outputs, and a time policy for period averages.
/// </summary>
public sealed class DatasetMeasureInputValidator : AbstractValidator<DatasetMeasureInput>
{
    /// <summary>The message of an implementation field on a measure that is not specialized.</summary>
    private const string OnlySpecializedUses = "Only specialized measures name an implementation, so '{PropertyName}' must be empty.";

    /// <summary>Every measure kind.</summary>
    private static readonly string[] Kinds = [.. MeasureKinds.Definition.Values.Select(value => value.Value)];

    /// <summary>The kinds that take no aggregation function.</summary>
    private static readonly string[] KindsWithoutFunction = [MeasureKinds.WeightedSum, MeasureKinds.WeightedAverage, MeasureKinds.PeriodAverage, MeasureKinds.Formula];

    /// <summary>Declares the rules.</summary>
    public DatasetMeasureInputValidator()
    {
        RuleFor(input => input.Id).MustBeChangeSetId();
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeKey(128);
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.MeasureKind).MustBeIn(MeasureKinds.Definition);
        RuleFor(input => input.AggregationFunction).MustBeIn(AggregationFunctions.Definition);
        RuleFor(input => input.ValueNullBehavior).MustBeIn(ValueNullBehaviors.Definition);
        RuleFor(input => input.NegativeWeightBehavior).MustBeIn(NegativeWeightBehaviors.Definition);
        RuleFor(input => input.EmptySetBehavior).MustBeIn(EmptySetBehaviors.Definition);
        RuleFor(input => input.BridgeFactorBehavior).MustBeIn(BridgeFactorBehaviors.Definition);
        RuleFor(input => input.BridgeAllocationBehavior).MustBeIn(BridgeAllocationBehaviors.Definition);
        RuleFor(input => input.TotalBehavior).MustBeIn(TotalBehaviors.Definition);
        RuleFor(input => input.OutputDataType).MustBeIn(MeasureOutputDataTypes.Definition);
        RuleFor(input => input.RoundingMode).MustBeIn(RoundingModes.Definition);
        RuleFor(input => input.UnitKey).MustFitVarchar(64);
        RuleFor(input => input.ExpressionText).MustFitVarchar(8192);
        RuleFor(input => input.RowFilterExpression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustFitVarchar(8192)
            .MustBeValidExpression()
            .Must(text => ExpressionInspector.Inspect(text!).MeasureKeys.Count == 0)
            .WithMessage("A row filter tests the fields of each source row, such as [f.status] = 'open'; it cannot reference measures.")
            .When(input => input.RowFilterExpression is not null);
        RuleFor(input => input.AccumulatorPrecision).InclusiveBetween(1, 38);
        RuleFor(input => input.AccumulatorScale)
            .InclusiveBetween(0, 38)
            .LessThanOrEqualTo(input => input.AccumulatorPrecision)
            .WithMessage("'{PropertyName}' must not exceed the accumulator precision.");

        RuleForEach(input => input.Components).ChildRules(component =>
        {
            component.RuleFor(item => item.Role).MustBeIn(ComponentRoles.Definition);
            component.RuleFor(item => item.FieldId).NotEmpty().MustBeChangeSetId();
        });
        RuleFor(input => input.Components)
            .Must(components => components.DistinctBy(component => component.Role, StringComparer.Ordinal).Count() == components.Count)
            .WithMessage("'{PropertyName}' must give each role once; a measure reads one field per role.");
        RuleFor(input => input.Components)
            .Custom((components, context) =>
            {
                var measure = context.InstanceToValidate;
                foreach (var problem in MeasureComponentRoles.Problems(measure.MeasureKind, measure.AggregationFunction, components.Select(component => component.Role))) context.AddFailure(problem);
            })
            .When(input => Kinds.Contains(input.MeasureKind));
        RuleFor(input => input.TimePolicy!).SetValidator(new DatasetMeasureTimePolicyValidator()).When(input => input.TimePolicy is not null);

        When(input => input.MeasureKind == MeasureKinds.Aggregate, () =>
        {
            RuleFor(input => input.AggregationFunction).NotNull().WithMessage(KindRequires(MeasureKinds.Aggregate));
        });
        When(input => KindsWithoutFunction.Contains(input.MeasureKind), () =>
        {
            RuleFor(input => input.AggregationFunction).Null().WithMessage(input => $"{input.MeasureKind} measures take no aggregation function, so '{{PropertyName}}' must be empty.");
        });
        When(input => input.MeasureKind == MeasureKinds.Formula, () =>
        {
            RuleFor(input => input.ExpressionText)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(KindRequires(MeasureKinds.Formula))
                .MustBeValidExpression()
                .Must(text => ExpressionInspector.Inspect(text!).FieldKeys.Count == 0)
                .WithMessage("A formula combines other measures of the version, such as [m.interest] / [m.balance]; it cannot reference fields.");
        }).Otherwise(() =>
        {
            RuleFor(input => input.ExpressionText).Null().WithMessage("Only formula measures have an expression, so '{PropertyName}' must be empty.");
        });
        When(input => input.MeasureKind == MeasureKinds.PeriodAverage, () =>
        {
            RuleFor(input => input.TimePolicy).NotNull().WithMessage(KindRequires(MeasureKinds.PeriodAverage));
        });
        When(input => input.MeasureKind == MeasureKinds.Specialized, () =>
        {
            RuleFor(input => input.ImplementationKey).NotEmpty().WithMessage(KindRequires(MeasureKinds.Specialized)).MustFitVarchar(128);
            RuleFor(input => input.ImplementationVersion).NotEmpty().WithMessage(KindRequires(MeasureKinds.Specialized)).MustFitVarchar(32);
            RuleFor(input => input.ConfigurationJson).MustBeJson();
        }).Otherwise(() =>
        {
            RuleFor(input => input.ImplementationKey).Null().WithMessage(OnlySpecializedUses);
            RuleFor(input => input.ImplementationVersion).Null().WithMessage(OnlySpecializedUses);
            RuleFor(input => input.ConfigurationJson).Null().WithMessage(OnlySpecializedUses);
        });
        When(input => input.OutputDataType == MeasureOutputDataTypes.Decimal, () =>
        {
            RuleFor(input => input.OutputPrecision).MustBeDecimalPrecision();
            RuleFor(input => input.OutputScale).MustBeDecimalScale(input => input.OutputPrecision, "output precision");
        }).Otherwise(() =>
        {
            RuleFor(input => input.OutputPrecision).Null().WithMessage("Only decimal outputs have a precision, so '{PropertyName}' must be empty.");
            RuleFor(input => input.OutputScale).Null().WithMessage("Only decimal outputs have a scale, so '{PropertyName}' must be empty.");
        });
    }

    /// <summary>The message of a value a kind requires.</summary>
    private static string KindRequires(string measureKind)
    {
        return $"'{{PropertyName}}' is required for {measureKind} measures.";
    }
}
