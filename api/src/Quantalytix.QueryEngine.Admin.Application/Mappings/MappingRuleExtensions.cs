using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>FluentValidation rules of the mapping entry inputs, one of which the bridge mapping list shares.</summary>
public static class MappingRuleExtensions
{
    /// <summary>Requires an actual source value: not empty and within the 512-byte column. The value is kept exactly as given.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a source value property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeSourceKeyValue<T>(this IRuleBuilder<T, string> rule)
    {
        return rule.NotEmpty().MustFitVarchar(512);
    }

    /// <summary>
    /// Reports each member ID of a list that no member has, once, with one read for the whole list, so a bulk paste costs one
    /// query. Whether the members belong to the right dimension version is for the publication rules to decide.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <typeparam name="TItem">The list's item type.</typeparam>
    /// <param name="rule">The rule for the list property of the input.</param>
    /// <param name="members">Reads the members.</param>
    /// <param name="memberIds">The member IDs one item references.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptionsConditions<T, IReadOnlyList<TItem>> MustReferenceExistingMembers<T, TItem>(
        this IRuleBuilder<T, IReadOnlyList<TItem>> rule,
        ITableGateway<DimensionMemberRecord> members,
        Func<TItem, IEnumerable<long>> memberIds)
    {
        return rule.CustomAsync(async (items, context, cancellationToken) =>
        {
            long[] referenced = [.. items.SelectMany(memberIds).Distinct()];
            var existing = (await members.ListAsync(new { MemberId = referenced }, cancellationToken)).Select(member => member.MemberId).ToHashSet();
            foreach (var missing in referenced.Where(memberId => !existing.Contains(memberId))) context.AddFailure($"Member {missing} does not exist.");
        });
    }
}
