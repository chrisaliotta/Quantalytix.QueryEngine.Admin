using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>entry_member_in_version</c>: every entry maps to a member of the pinned dimension version. Member IDs are global, so a
/// foreign key alone cannot keep an entry inside the version the engine resolves.
/// </summary>
public sealed class EntryMemberInVersionRule(MappingEntrySetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "entry_member_in_version";

    /// <summary>The kind the rule checks: mapping versions.</summary>
    public string Kind { get; } = VersionKinds.MappingVersion;

    /// <summary>Reports every entry whose member is not a member of the pinned dimension version.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per such entry.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        return
        [
            .. set.Entries
                .Where(entry => !set.Members.ContainsKey(entry.MemberId))
                .Select(entry => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Source key '{entry.SourceKeyValue}' maps to member {entry.MemberId}, which is not a member of the pinned dimension version."
                })
        ];
    }
}
