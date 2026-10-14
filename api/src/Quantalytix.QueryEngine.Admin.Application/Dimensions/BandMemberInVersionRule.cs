using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary><c>band_member_in_version</c>: every band assigns a member of its own version, since member IDs are global.</summary>
public sealed class BandMemberInVersionRule(RangeBandSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "band_member_in_version";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports every band whose member is not a member of the version.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per such band.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        return
        [
            .. set.Bands
                .Where(band => !set.MemberKeys.ContainsKey(band.MemberId))
                .Select(band => new RuleViolation { Rule = Code, Message = $"{set.Describe(band)} assigns a member that is not a member of this version." })
        ];
    }
}
