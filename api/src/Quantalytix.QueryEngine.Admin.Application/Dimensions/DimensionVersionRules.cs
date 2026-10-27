using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The dimension snapshot rules as the version workflow's one <c>dimension_version</c> publication rule, so the workflow's checks
/// (the freeze of <see cref="LivingVersionFreezer"/>, and the publish of a report dimension's version) run the same rules on the
/// stored version that a change set runs on its pending result.
/// </summary>
/// <param name="loader">Reads the version as a snapshot.</param>
/// <param name="rules">Runs every dimension rule.</param>
public sealed class DimensionVersionRules(DimensionSnapshotLoader loader, DimensionRules rules) : IPublicationRule
{
    /// <summary><c>dimension_version</c>.</summary>
    public string Kind => VersionKinds.DimensionVersion;

    /// <summary>Runs every dimension rule on a stored version.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync(await loader.LoadVersionAsync(versionId, cancellationToken), cancellationToken);
    }
}
