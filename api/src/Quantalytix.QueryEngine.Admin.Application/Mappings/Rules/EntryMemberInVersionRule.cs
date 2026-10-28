using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>entry_member_in_version</c>: every entry maps to a member key of the live version of the set's dimension, which the active
/// assumption set pins and the engine resolves entries in (B3). A dimension without a live version is
/// <c>dimension_version_published</c>'s concern.
/// </summary>
public sealed class EntryMemberInVersionRule : MappingRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "entry_member_in_version";

    /// <summary>Reports every entry whose member key the live dimension version lacks.</summary>
    /// <param name="snapshot">The mapping version.</param>
    /// <returns>One violation per such entry.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(MappingSetSnapshot snapshot)
    {
        if (snapshot.Surroundings.Published is not { } published) return [];
        return snapshot.Entries
            .Where(entry => !published.MemberKeys.Contains(entry.MemberKey))
            .Select(entry => MappingViolations.Of(
                Code,
                $"Source key '{entry.SourceKeyValue}' maps to member '{entry.MemberKey}', which is not a member of v{published.Version.VersionNumber}, the live version of the set's dimension.",
                MappingAreas.Entries,
                entry.DimensionMappingId,
                filter: entry.MemberKey));
    }
}
