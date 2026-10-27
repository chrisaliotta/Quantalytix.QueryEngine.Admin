using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Checks a formula check request: a formula that fits <c>expression_text VARCHAR(4096)</c>; its meaning is the check's answer.</summary>
public sealed class ModelFormulaCheckRequestValidator : AbstractValidator<ModelFormulaCheckRequest>
{
    /// <summary>Declares the rules.</summary>
    public ModelFormulaCheckRequestValidator()
    {
        RuleFor(request => request.Formula).MustFitVarchar(4096);
    }
}
