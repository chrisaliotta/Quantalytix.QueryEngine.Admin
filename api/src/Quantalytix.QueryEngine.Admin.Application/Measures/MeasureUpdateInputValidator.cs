using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Checks a measure against schema sections 13 and 14: vocabulary values; the aggregation function, formula, component
/// roles, time policy and implementation that each measure kind requires and forbids; a formula over measures only and a
/// row filter over fields only, both parsed by the query engine; a precision and scale for decimal outputs; and an
/// accumulator precision and scale that StarRocks decimals support. Whether the components' fields and the formula's
/// measures belong to the version is left to the publication rules, which see the route too.
/// </summary>
public sealed class MeasureUpdateInputValidator : AbstractValidator<MeasureUpdateInput>
{
    /// <summary>The largest decimal precision StarRocks supports.</summary>
    private const int MaxPrecision = 38;

    /// <summary>The message of an implementation attribute on a measure that is not specialized.</summary>
    private const string OnlySpecializedUses = "Only specialized measures name an implementation, so '{PropertyName}' must be empty.";

    /// <summary>Every <c>measure_kind</c> value.</summary>
    private static readonly string[] Kinds = [.. MeasureKinds.Definition.Values.Select(value => value.Value)];

    /// <summary>The measure kinds that aggregate without an aggregation function.</summary>
    private static readonly string[] KindsWithoutFunction =
        [MeasureKinds.WeightedSum, MeasureKinds.WeightedAverage, MeasureKinds.PeriodAverage, MeasureKinds.Formula];

    /// <summary>Declares the rules.</summary>
    public MeasureUpdateInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MaximumLength(256);
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
        RuleFor(input => input.UnitKey).MaximumLength(64);
        RuleFor(input => input.ExpressionText).MaximumLength(8192);
        RuleFor(input => input.RowFilterExpression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(8192)
            .MustBeValidExpression()
            .Must(text => ExpressionInspector.Inspect(text!).MeasureKeys.Count == 0)
            .WithMessage("A row filter tests the fields of each source row, such as [f.status] = 'open'; it cannot reference measures.")
            .When(input => input.RowFilterExpression is not null);
        RuleFor(input => input.AccumulatorPrecision).NotNull().InclusiveBetween(1, MaxPrecision);
        RuleFor(input => input.AccumulatorScale)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .GreaterThanOrEqualTo(0)
            .Must((input, scale) => scale <= input.AccumulatorPrecision)
            .WithMessage("'{PropertyName}' must not exceed the accumulator precision.");

        RuleForEach(input => input.Components).SetValidator(new MeasureComponentInputValidator());
        RuleFor(input => input.Components)
            .Must(components => components.DistinctBy(component => component.ComponentRole, StringComparer.Ordinal).Count() == components.Count)
            .WithMessage("'{PropertyName}' must give each role once; a measure reads one field per role.");
        RuleFor(input => input.Components)
            .Custom((components, context) =>
            {
                var roles = components.Select(component => component.ComponentRole);
                foreach (var problem in MeasureComponentRoles.Problems(context.InstanceToValidate.MeasureKind, roles)) context.AddFailure(problem);
            })
            .When(input => Kinds.Contains(input.MeasureKind));
        RuleFor(input => input.TimePolicy!).SetValidator(new MeasureTimePolicyInputValidator());

        When(input => input.MeasureKind == MeasureKinds.Aggregate, () =>
        {
            RuleFor(input => input.AggregationFunction).NotNull().WithMessage(KindRequires(MeasureKinds.Aggregate));
        });
        When(input => KindsWithoutFunction.Contains(input.MeasureKind), () =>
        {
            RuleFor(input => input.AggregationFunction)
                .Null()
                .WithMessage(input => $"{input.MeasureKind} measures take no aggregation function, so '{{PropertyName}}' must be empty.");
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
            RuleFor(input => input.ImplementationKey).NotEmpty().WithMessage(KindRequires(MeasureKinds.Specialized)).MaximumLength(128);
            RuleFor(input => input.ImplementationVersion).NotEmpty().WithMessage(KindRequires(MeasureKinds.Specialized)).MaximumLength(32);
            RuleFor(input => input.ConfigurationJson).MustBeJson();
        }).Otherwise(() =>
        {
            RuleFor(input => input.ImplementationKey).Null().WithMessage(OnlySpecializedUses);
            RuleFor(input => input.ImplementationVersion).Null().WithMessage(OnlySpecializedUses);
            RuleFor(input => input.ConfigurationJson).Null().WithMessage(OnlySpecializedUses);
        });

        When(input => input.OutputDataType == MeasureOutputDataTypes.Decimal, () =>
        {
            RuleFor(input => input.OutputPrecision).NotNull().InclusiveBetween(1, MaxPrecision);
            RuleFor(input => input.OutputScale)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .GreaterThanOrEqualTo(0)
                .Must((input, scale) => scale <= input.OutputPrecision)
                .WithMessage("'{PropertyName}' must not exceed the output precision.");
        }).Otherwise(() =>
        {
            RuleFor(input => input.OutputPrecision).Null().WithMessage("Only decimal outputs have a precision, so '{PropertyName}' must be empty.");
            RuleFor(input => input.OutputScale).Null().WithMessage("Only decimal outputs have a scale, so '{PropertyName}' must be empty.");
        });
    }

    /// <summary>The message of an attribute that a measure kind requires.</summary>
    private static string KindRequires(string measureKind)
    {
        return $"'{{PropertyName}}' is required for {measureKind} measures.";
    }
}
