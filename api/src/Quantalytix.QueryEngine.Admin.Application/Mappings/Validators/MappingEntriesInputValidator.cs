using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks a complete entry list: every source value and member key fits its column, and no source value appears twice,
/// because a source value maps to exactly one member within a mapping version. Values compare exactly, so <c>003000</c> and
/// <c>3000</c> are different.
/// </summary>
public sealed class MappingEntriesInputValidator : AbstractValidator<MappingEntriesInput>
{
    /// <summary>Declares the rules.</summary>
    public MappingEntriesInputValidator()
    {
        RuleForEach(input => input.Entries).SetValidator(new MappingEntryInputValidator());
        RuleFor(input => input.Entries).Custom((entries, context) =>
        {
            var repeated = entries
                .GroupBy(entry => entry.SourceKeyValue, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            foreach (var sourceKey in repeated) context.AddFailure($"Source key '{sourceKey}' appears more than once.");
        });
    }
}
