using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The bridge snapshot rules as the version workflow's one <c>bridge_version</c> publication rule, so the freeze of
/// <see cref="LivingVersionFreezer"/> and the publish of a new live version 1 (analytical or report) run the same rules on the stored
/// version that a change set runs on its pending result.
/// </summary>
/// <param name="loader">Reads the version as a snapshot.</param>
/// <param name="rules">Runs every bridge rule.</param>
public sealed class BridgeVersionRules(BridgeSnapshotLoader loader, SnapshotRuleRunner<BridgeSnapshot> rules) : IPublicationRule
{
    /// <summary><c>bridge_version</c>.</summary>
    public string Kind => VersionKinds.BridgeVersion;

    /// <summary>Runs every bridge rule on a stored version.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync(await loader.LoadVersionAsync(versionId, cancellationToken), cancellationToken);
    }
}
