using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks each model operation's input shape before it applies: keys, names and formulas that fit their columns, known vocabulary
/// values, and a parameter's defaults as its type names them. What the input means for the model (a taken key, a formula reading a
/// metric the model lacks) is decided as the operation applies and by the model gate.
/// </summary>
public sealed class ModelOperationValidator : AbstractValidator<ModelOperation>
{
    /// <summary>Declares the rules.</summary>
    public ModelOperationValidator()
    {
        RuleFor(operation => operation).SetInheritanceValidator(operations =>
        {
            operations.Add(new InlineValidator<ModelUpdateModelOperation>
            {
                validator => validator.RuleFor(operation => operation.Patch)
                    .Must(patch => patch != new CalcModelPatch())
                    .WithMessage("Name at least one setting to change."),
                validator => validator.RuleFor(operation => operation.Patch.DisplayName).MustBeModelName().When(operation => operation.Patch.DisplayName is not null),
                validator => validator.RuleFor(operation => operation.Patch.CurrencyCode).MustBeCurrencyCode().When(operation => operation.Patch.CurrencyCode is not null),
                validator => validator.RuleFor(operation => operation.Patch.CalendarKey)
                    .Equal(ModelCalendars.Gregorian)
                    .WithMessage($"'{{PropertyName}}' must be '{ModelCalendars.Gregorian}', the calendar every model follows.")
                    .When(operation => operation.Patch.CalendarKey is not null),
                validator => validator.RuleFor(operation => operation.Patch.FormulaLanguage).MustBeIn(FormulaLanguageVersions.Definition),
                validator => validator.RuleFor(operation => operation.Patch.AttributionMethod).MustBeIn(AttributionMethods.Definition)
            });
            operations.Add(new InlineValidator<ModelSaveVariableOperation> { validator => validator.RuleFor(operation => operation.Input).SetValidator(new ModelVariableValidator()) });
            operations.Add(new InlineValidator<ModelDeleteVariableOperation> { validator => validator.RuleFor(operation => operation.Key).NotEmpty() });
            operations.Add(new InlineValidator<ModelSaveMeasureOperation> { validator => validator.RuleFor(operation => operation.Input).SetValidator(new ModelMeasureValidator()) });
            operations.Add(new InlineValidator<ModelDeleteMeasureOperation> { validator => validator.RuleFor(operation => operation.Key).NotEmpty() });
            operations.Add(new InlineValidator<ModelSaveParameterOperation> { validator => validator.RuleFor(operation => operation.Input).SetValidator(new ModelParameterItemValidator()) });
            operations.Add(new InlineValidator<ModelDeleteParameterOperation> { validator => validator.RuleFor(operation => operation.Key).NotEmpty() });
            operations.Add(new InlineValidator<ModelSetAttributionOperation> { validator => validator.RuleFor(operation => operation.Input).SetValidator(new AttributionValidator()) });
            operations.Add(new InlineValidator<ModelClearAttributionOperation> { validator => validator.RuleFor(operation => operation.MetricKey).NotEmpty() });
        });
    }
}
