using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The shape of a field input (D5): a column field names its column, a JSON path field its JSON column and path, an expression field
/// a valid row expression over fields; only decimals carry precision and scale; <c>createColumn</c> is for column fields and
/// <c>materialize</c> for JSON path fields.
/// </summary>
public sealed class DatasetFieldInputValidator : AbstractValidator<DatasetFieldInput>
{
    /// <summary>Declares the rules.</summary>
    public DatasetFieldInputValidator()
    {
        RuleFor(input => input.Id).MustBeChangeSetId();
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeKey(128);
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.FieldSourceKind).MustBeIn(FieldSourceKinds.Definition);
        this.RequireFieldType(input => input.DataType, input => input.NumericPrecision, input => input.NumericScale);
        RuleFor(input => input.SourceColumn).MustBeIdentifier();
        RuleFor(input => input.JsonPath).MaximumLength(512).Matches(DatasetPatterns.JsonPath()).WithMessage(DatasetPatterns.JsonPathMessage);
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
                .MustFitVarchar(8192)
                .MustBeValidExpression()
                .Must(text => ExpressionInspector.Inspect(text!).MeasureKeys.Count == 0)
                .WithMessage("A field expression reads fields, such as [f.balance]; it cannot reference measures.");
            RuleFor(input => input.SourceColumn).Null().WithMessage(Forbidden(FieldSourceKinds.Expression));
            RuleFor(input => input.JsonPath).Null().WithMessage(Forbidden(FieldSourceKinds.Expression));
        });

        RuleFor(input => input.CreateColumn)
            .NotEqual(true)
            .WithMessage("Only column fields create their column.")
            .When(input => input.FieldSourceKind != FieldSourceKinds.Column);
        RuleFor(input => input.Materialize)
            .NotEqual(true)
            .WithMessage("Only JSON path fields are materialized.")
            .When(input => input.FieldSourceKind != FieldSourceKinds.JsonPath);
    }

    /// <summary>The message of a value a source kind requires.</summary>
    private static string Required(string sourceKind)
    {
        return $"'{{PropertyName}}' is required for a {sourceKind} field.";
    }

    /// <summary>The message of a value a source kind does not read.</summary>
    private static string Forbidden(string sourceKind)
    {
        return $"A {sourceKind} field does not read '{{PropertyName}}', so it must be empty.";
    }
}
