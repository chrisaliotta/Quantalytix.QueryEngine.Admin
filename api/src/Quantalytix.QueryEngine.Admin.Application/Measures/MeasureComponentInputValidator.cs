using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>Checks one component of a measure: its role is a <c>component_role</c> value and it names a field.</summary>
public sealed class MeasureComponentInputValidator : AbstractValidator<MeasureComponentInput>
{
    /// <summary>Declares the rules.</summary>
    public MeasureComponentInputValidator()
    {
        RuleFor(input => input.ComponentRole).MustBeIn(ComponentRoles.Definition);
        RuleFor(input => input.FieldId).NotNull();
    }
}
