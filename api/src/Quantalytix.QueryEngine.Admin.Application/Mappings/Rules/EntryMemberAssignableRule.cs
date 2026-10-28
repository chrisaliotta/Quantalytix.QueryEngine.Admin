using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>entry_member_assignable</c>: entries map only to assignable members (B3), because folder-only members such as a country above
/// its states must not receive source values directly. Members outside the live version are <c>entry_member_in_version</c>'s concern.
/// </summary>
public sealed class EntryMemberAssignableRule : MappingRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "entry_member_assignable";

    /// <summary>Reports every entry that maps to a member of the live version whose <c>is_assignable</c> is false.</summary>
    /// <param name="snapshot">The mapping version.</param>
    /// <returns>One violation per such entry.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(MappingSetSnapshot snapshot)
    {
        if (snapshot.Surroundings.Published is not { } published) return [];
        return snapshot.Entries
            .Where(entry => published.UnassignableMemberKeys.Contains(entry.MemberKey))
            .Select(entry => MappingViolations.Of(
                Code,
                $"Source key '{entry.SourceKeyValue}' maps to member '{entry.MemberKey}', which is not assignable.",
                MappingAreas.Entries,
                entry.DimensionMappingId,
                filter: entry.MemberKey));
    }
}
