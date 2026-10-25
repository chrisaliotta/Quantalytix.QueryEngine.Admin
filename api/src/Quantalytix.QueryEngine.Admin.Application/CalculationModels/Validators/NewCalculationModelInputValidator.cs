using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Checks a new calculation model: its identity attributes and a unique, well-formed key.</summary>
public sealed class NewCalculationModelInputValidator : AbstractValidator<NewCalculationModelInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="models">Reads existing model keys.</param>
    public NewCalculationModelInputValidator(ITableGateway<CalculationModelRecord> models)
    {
        Include(new CalculationModelInputValidator());
        RuleFor(input => input.ModelKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await models.ListAsync(new { ModelKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"A calculation model with key '{input.ModelKey}' already exists.");
    }
}
