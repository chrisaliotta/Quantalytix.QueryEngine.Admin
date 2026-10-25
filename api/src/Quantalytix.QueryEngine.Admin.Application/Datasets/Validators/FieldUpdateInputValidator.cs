using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Checks a field's editable attributes against schema section 7: the source attributes each source kind requires and
/// forbids, an expression that parses and reads only fields, the precision and scale that only decimals carry, and the
/// column name and JSON path patterns that make both safe to inline into DDL.
/// </summary>
public sealed class FieldUpdateInputValidator : AbstractValidator<FieldUpdateInput>
{
    /// <summary>Declares the rules.</summary>
    public FieldUpdateInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.FieldSourceKind).MustBeIn(FieldSourceKinds.Definition);
        RuleFor(input => input.DataType).MustBeIn(DataTypes.Definition);
        RuleFor(input => input.SourceColumn)
            .MaximumLength(128)
            .Matches(DatasetPatterns.ColumnName())
            .WithMessage(DatasetPatterns.ColumnNameMessage);
        RuleFor(input => input.JsonPath)
            .MaximumLength(512)
            .Matches(DatasetPatterns.JsonPath())
            .WithMessage(DatasetPatterns.JsonPathMessage);
        RuleFor(input => input.ExpressionText).MustFitVarchar(8192);
        RuleFor(input => input.UnitKey).MustFitVarchar(64);

        When(input => input.FieldSourceKind == FieldSourceKinds.Column, () =>
        {
            RuleFor(input => input.SourceColumn).NotEmpty().WithMessage(Required(FieldSourceKinds.Column));
            RuleFor(input => input.JsonPath).Null().WithMessage(Forbidden(FieldSourceKinds.Column));
            RuleFor(input => input.ExpressionText).Null().WithMessage(Forbidden(FieldSourceKinds.Column));
        });
        When(input => input.FieldSourceKind == FieldSourceKinds.JsonPath, () =>
        {
            RuleFor(input => input.SourceColumn).NotEmpty().WithMessage(Required(FieldSourceKinds.JsonPath));
            RuleFor(input => input.JsonPath).NotEmpty().WithMessage(Required(FieldSourceKinds.JsonPath));
            RuleFor(input => input.ExpressionText).Null().WithMessage(Forbidden(FieldSourceKinds.JsonPath));
        });
        When(input => input.FieldSourceKind == FieldSourceKinds.Expression, () =>
        {
            RuleFor(input => input.ExpressionText)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(Required(FieldSourceKinds.Expression))
                .MustBeValidExpression()
                .Must(text => ExpressionInspector.Inspect(text!).MeasureKeys.Count == 0)
                .WithMessage("A field expression reads fields, such as [f.balance]; it cannot reference measures.");
            RuleFor(input => input.SourceColumn).Null().WithMessage(Forbidden(FieldSourceKinds.Expression));
            RuleFor(input => input.JsonPath).Null().WithMessage(Forbidden(FieldSourceKinds.Expression));
        });

        When(input => input.DataType == DataTypes.Decimal, () =>
        {
            RuleFor(input => input.NumericPrecision).MustBeDecimalPrecision();
            RuleFor(input => input.NumericScale).MustBeDecimalScale(input => input.NumericPrecision, "precision");
        }).Otherwise(() =>
        {
            RuleFor(input => input.NumericPrecision).Null().WithMessage("Only decimal fields have a precision, so '{PropertyName}' must be empty.");
            RuleFor(input => input.NumericScale).Null().WithMessage("Only decimal fields have a scale, so '{PropertyName}' must be empty.");
        });
    }

    /// <summary>The message of a source attribute that a source kind requires.</summary>
    private static string Required(string sourceKind)
    {
        return $"'{{PropertyName}}' is required for a {sourceKind} field.";
    }

    /// <summary>The message of a source attribute that a source kind does not read.</summary>
    private static string Forbidden(string sourceKind)
    {
        return $"A {sourceKind} field does not read '{{PropertyName}}', so it must be empty.";
    }
}
