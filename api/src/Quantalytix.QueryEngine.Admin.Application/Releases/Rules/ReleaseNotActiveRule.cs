using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>release_not_active</c>: the release the tenant's queries run on cannot be retired. The query engine reads the active release
/// among published rows only, so retiring it would leave every query without one; another release must be activated first. The
/// release's retire command and the version workflow both run it.
/// </summary>
public sealed class ReleaseNotActiveRule(TenantInfoReader tenant, ITableGateway<SemanticReleaseRecord> releases) : IRetirementRule
{
    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports the release <c>active_semantic_release_id</c> points at.</summary>
    /// <param name="versionId">The release being retired.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation for the active release; none otherwise.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        if ((await tenant.ReadAsync(cancellationToken)).ActiveReleaseId != versionId) return [];
        var release = await releases.GetAsync(new { SemanticReleaseId = versionId }, cancellationToken);
        return
        [
            new RuleViolation
            {
                Code = "release_not_active",
                Message = $"Release '{release.ReleaseKey}' is the active release; activate another release before retiring it."
            }
        ];
    }
}
