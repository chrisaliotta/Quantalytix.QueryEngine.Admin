using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Checks a new grain: a well-formed key plus every rule of <see cref="GrainUpdateInputValidator"/>. Whether the key is
/// unique within the version is the <c>grain_key_unique</c> publication rule, because the version comes from the route.
/// </summary>
public sealed class GrainInputValidator : AbstractValidator<GrainInput>
{
    /// <summary>Declares the rules.</summary>
    public GrainInputValidator()
    {
        RuleFor(input => input.GrainKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(DatasetPatterns.Key())
            .WithMessage(DatasetPatterns.KeyMessage);
        Include(new GrainUpdateInputValidator());
    }
}
