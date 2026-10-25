using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>member_level_in_version</c>: a member's level belongs to the same version, because level grouping matches levels by
/// raw ID within the resolved version.
/// </summary>
public sealed class MemberLevelInVersionRule(
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<DimensionLevelRecord> levels) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "member_level_in_version";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports every member whose level is not a level of the version.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per such member.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await members.ListAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var levelIds = (await levels.ListAsync(new { DimensionVersionId = versionId }, cancellationToken)).Select(level => level.DimensionLevelId).ToHashSet();
        return
        [
            .. rows
                .Where(member => member.DimensionLevelId is { } dimensionLevelId && !levelIds.Contains(dimensionLevelId))
                .OrderBy(member => member.MemberKey, StringComparer.Ordinal)
                .Select(member => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Member '{member.MemberKey}' has level {member.DimensionLevelId}, which is not a level of this version."
                })
        ];
    }
}
