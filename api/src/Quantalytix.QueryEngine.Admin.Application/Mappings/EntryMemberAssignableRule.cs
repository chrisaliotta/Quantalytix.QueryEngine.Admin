using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>entry_member_assignable</c>: entries map only to assignable members, because folder-only members such as a country
/// above its states must not receive source values directly. Members outside the version are
/// <c>entry_member_in_version</c>'s concern.
/// </summary>
public sealed class EntryMemberAssignableRule(MappingEntrySetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "entry_member_assignable";

    /// <summary>The kind the rule checks: mapping versions.</summary>
    public string Kind { get; } = VersionKinds.MappingVersion;

    /// <summary>Reports every entry that maps to a member of the version whose <c>is_assignable</c> is false.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per such entry.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        return
        [
            .. set.Entries
                .Where(entry => set.Members.TryGetValue(entry.MemberId, out var member) && !member.IsAssignable)
                .Select(entry => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Source key '{entry.SourceKeyValue}' maps to member '{set.Members[entry.MemberId].MemberKey}', which is not assignable."
                })
        ];
    }
}
