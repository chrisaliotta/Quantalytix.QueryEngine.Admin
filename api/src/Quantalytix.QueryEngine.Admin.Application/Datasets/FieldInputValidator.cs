using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Checks a new field: a well-formed key plus every rule of <see cref="FieldUpdateInputValidator"/>. Whether the key is
/// unique within the version is the <c>field_key_unique</c> publication rule, because the version comes from the route.
/// </summary>
public sealed class FieldInputValidator : AbstractValidator<FieldInput>
{
    /// <summary>Declares the rules.</summary>
    public FieldInputValidator()
    {
        RuleFor(input => input.FieldKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(DatasetPatterns.Key())
            .WithMessage(DatasetPatterns.KeyMessage);
        Include(new FieldUpdateInputValidator());
    }
}
