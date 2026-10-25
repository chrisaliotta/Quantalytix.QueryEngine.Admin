using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>member_parent_in_version</c>: a member's parent belongs to the same version. Member IDs are global, so a foreign
/// key alone cannot keep a hierarchy inside its version.
/// </summary>
public sealed class MemberParentInVersionRule(ITableGateway<DimensionMemberRecord> members) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "member_parent_in_version";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports every member whose parent is not a member of the version.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per such member.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await members.ListAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var memberIds = rows.Select(member => member.DimensionMemberId).ToHashSet();
        return
        [
            .. rows
                .Where(member => member.ParentDimensionMemberId is { } parentId && !memberIds.Contains(parentId))
                .OrderBy(member => member.MemberKey, StringComparer.Ordinal)
                .Select(member => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Member '{member.MemberKey}' has parent {member.ParentDimensionMemberId}, which is not a member of this version."
                })
        ];
    }
}
