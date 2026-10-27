using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>entry_member_in_version</c>: every entry maps to a member key of the published version of the set's dimension, which the
/// active assumption set pins and the engine resolves entries in. A dimension with no published version is
/// <c>dimension_version_published</c>'s concern.
/// </summary>
public sealed class EntryMemberInVersionRule(MappingEntrySetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "entry_member_in_version";

    /// <summary>The kind the rule checks: mapping versions.</summary>
    public string Kind { get; } = VersionKinds.MappingVersion;

    /// <summary>Reports every entry whose member key the published dimension version lacks.</summary>
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
                .Where(entry => !dimension.MemberKeys.Contains(entry.MemberKey))
                .Select(entry => new RuleViolation
                {
                    Code = Code,
                    Message = $"Source key '{entry.SourceKeyValue}' maps to member '{entry.MemberKey}', which is not a member of v{dimension.Version.VersionNumber}, the published version of the set's dimension."
                })
        ];
    }
}
