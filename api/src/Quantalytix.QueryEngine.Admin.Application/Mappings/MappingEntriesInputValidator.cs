using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks a complete entry list: every source value fits its column, no source value appears twice, because a source value
/// maps to exactly one member within a mapping version, and every member exists. Values compare exactly, so <c>003000</c>
/// and <c>3000</c> are different.
/// </summary>
public sealed class MappingEntriesInputValidator : AbstractValidator<MappingEntriesInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="members">Reads the members the entries map to.</param>
    public MappingEntriesInputValidator(ITableGateway<DimensionMemberRecord> members)
    {
        RuleForEach(input => input.Entries).ChildRules(entry => entry.RuleFor(input => input.SourceKeyValue).MustBeSourceKeyValue());
        RuleFor(input => input.Entries).Custom((entries, context) =>
        {
            var repeated = entries
                .GroupBy(entry => entry.SourceKeyValue, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            foreach (var sourceKey in repeated) context.AddFailure($"Source key '{sourceKey}' appears more than once.");
        });
        RuleFor(input => input.Entries).MustReferenceExistingMembers(members, entry => [entry.MemberId]);
    }
}
