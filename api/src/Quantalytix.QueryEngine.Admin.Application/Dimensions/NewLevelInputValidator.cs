using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Checks a new level: its attributes and a well-formed key.</summary>
public sealed class NewLevelInputValidator : AbstractValidator<NewLevelInput>
{
    /// <summary>Declares the rules.</summary>
    public NewLevelInputValidator()
    {
        Include(new LevelInputValidator());
        RuleFor(input => input.LevelKey).Cascade(CascadeMode.Stop).MustBeKey(128);
    }
}
