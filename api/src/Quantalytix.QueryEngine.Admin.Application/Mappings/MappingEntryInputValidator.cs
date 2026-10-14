using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks one entry's source value: present and within the 512-character column. Whether its member belongs to the pinned
/// dimension version and is assignable are publication rules, because they depend on the version.
/// </summary>
public sealed class MappingEntryInputValidator : AbstractValidator<MappingEntryInput>
{
    /// <summary>Declares the rules.</summary>
    public MappingEntryInputValidator()
    {
        RuleFor(input => input.SourceKeyValue).NotEmpty().MaximumLength(512);
    }
}
