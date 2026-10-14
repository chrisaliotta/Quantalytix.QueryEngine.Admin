using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Checks a level's editable attributes; <see cref="NewLevelInputValidator"/> includes these rules.</summary>
public sealed class LevelInputValidator : AbstractValidator<LevelInput>
{
    /// <summary>Declares the rules.</summary>
    public LevelInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(input => input.Periodicity).MustBeIn(Periodicities.Definition);
    }
}
