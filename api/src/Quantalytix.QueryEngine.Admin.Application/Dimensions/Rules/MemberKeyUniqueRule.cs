using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>member_key_unique</c>: member keys are unique within the version, because the engine finds a source value's member by
/// its key, and two members with one key would duplicate fact rows.
/// </summary>
public sealed class MemberKeyUniqueRule(ITableGateway<DimensionMemberRecord> members) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "member_key_unique";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports every member key that more than one member of the version uses.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per repeated key.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await members.ListAsync(new { DimensionVersionId = versionId }, cancellationToken);
        return
        [
            .. rows
                .GroupBy(member => member.MemberKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuleViolation { Code = Code, Message = $"Member key '{group.Key}' is used by {group.Count()} members." })
        ];
    }
}
