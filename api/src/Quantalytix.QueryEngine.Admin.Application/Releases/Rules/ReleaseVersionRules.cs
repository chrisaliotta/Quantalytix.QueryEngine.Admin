using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// The manifest rules as the version workflow's <c>semantic_release</c> publication rule, so a release the generic status endpoint
/// moves runs the same rules as the release's own lifecycle endpoints.
/// </summary>
/// <param name="loader">Reads the release as a snapshot.</param>
/// <param name="rules">Runs the release rules.</param>
public sealed class ReleaseVersionRules(ReleaseSnapshotLoader loader, ReleaseRules rules) : IPublicationRule
{
    /// <summary><c>semantic_release</c>.</summary>
    public string Kind => VersionKinds.SemanticRelease;

    /// <summary>Runs every manifest rule on a stored release.</summary>
    /// <param name="versionId">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadAsync(versionId, cancellationToken);
        return [.. (await rules.EvaluateAsync(snapshot, cancellationToken)).Select(violation => violation.ToRuleViolation())];
    }
}
