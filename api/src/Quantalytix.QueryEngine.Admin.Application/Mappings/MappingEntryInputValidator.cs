using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks one entry: a source value within the 512-byte column and a member key within its column. Whether the published
/// version of the set's dimension has the member, and whether it is assignable, are publication rules.
/// </summary>
public sealed class MappingEntryInputValidator : AbstractValidator<MappingEntryInput>
{
    /// <summary>Declares the rules.</summary>
    public MappingEntryInputValidator()
    {
        RuleFor(input => input.SourceKeyValue).MustBeSourceKeyValue();
        RuleFor(input => input.MemberKey).MustBeMemberKey();
    }
}
