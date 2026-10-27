using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>entry_member_assignable</c>: entries map only to assignable members, because folder-only members such as a country
/// above its states must not receive source values directly. Assignability is read in the published version of the set's
/// dimension; members outside it are <c>entry_member_in_version</c>'s concern.
/// </summary>
public sealed class EntryMemberAssignableRule(MappingEntrySetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "entry_member_assignable";

    /// <summary>The kind the rule checks: mapping versions.</summary>
    public string Kind { get; } = VersionKinds.MappingVersion;

    /// <summary>Reports every entry that maps to a member of the published version whose <c>is_assignable</c> is false.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per such entry.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        if (set.Dimension is not { } dimension) return [];
        return
        [
            .. set.Entries
                .Where(entry => dimension.UnassignableMemberKeys.Contains(entry.MemberKey))
                .Select(entry => new RuleViolation
                {
                    Code = Code,
                    Message = $"Source key '{entry.SourceKeyValue}' maps to member '{entry.MemberKey}', which is not assignable."
                })
        ];
    }
}
