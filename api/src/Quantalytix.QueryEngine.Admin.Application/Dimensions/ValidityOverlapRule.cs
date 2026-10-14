using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>validity_overlap</c>: the version's window must not overlap any published version of the same dimension, so the
/// engine resolves exactly one version per request time. An open-ended published predecessor that starts earlier is
/// allowed, because publishing this version closes it.
/// </summary>
public sealed class ValidityOverlapRule(ITableGateway<DimensionVersionRecord> versions) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "validity_overlap";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports every published version whose window overlaps this one's. Without a start there is no window yet.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per overlapping published version.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        if (version.ValidFrom is null) return [];
        var published = await versions.ListAsync(new { version.DimensionId, Status = PublicationStatuses.Published }, cancellationToken);
        return
        [
            .. published
                .Where(other => other.DimensionVersionId != versionId)
                .Where(other => !DimensionValidity.IsOpenPredecessor(other, version) && DimensionValidity.Overlap(other, version))
                .OrderBy(other => other.VersionNumber)
                .Select(other => new RuleViolation
                {
                    Rule = Code,
                    Message = $"{DimensionValidity.Describe(version)} overlaps published {DimensionValidity.Describe(other)}."
                })
        ];
    }
}
