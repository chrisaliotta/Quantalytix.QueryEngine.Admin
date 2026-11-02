using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>entry_member_in_version</c>: every entry maps to a member key of the set's dimension's version in the mapping version's
/// snapshot, which the engine resolves entries in (B3). A dimension without a version in the snapshot is
/// <c>dimension_version_in_snapshot</c>'s concern.
/// </summary>
public sealed class EntryMemberInVersionRule : MappingRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "entry_member_in_version";

    /// <summary>Reports every entry whose member key the dimension's version lacks.</summary>
    /// <param name="snapshot">The mapping version.</param>
    /// <returns>One violation per such entry.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(MappingSetSnapshot snapshot)
    {
        if (snapshot.Surroundings.DimensionKeys is not { } keys) return [];
        return snapshot.Entries
            .Where(entry => !keys.MemberKeys.Contains(entry.MemberKey))
            .Select(entry => MappingViolations.Of(
                Code,
                $"Source key '{entry.SourceKeyValue}' maps to member '{entry.MemberKey}', which is not a member of the set's dimension.",
                MappingAreas.Entries,
                entry.DimensionMappingId,
                filter: entry.MemberKey));
    }
}
