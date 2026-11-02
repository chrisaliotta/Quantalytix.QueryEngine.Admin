using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The bridge snapshot rules as the bridge kind's <see cref="IVersionRules"/>, which restore runs on a stored version.</summary>
/// <param name="loader">Reads the version as a snapshot.</param>
/// <param name="rules">Runs every bridge rule.</param>
public sealed class BridgeVersionRules(BridgeSnapshotLoader loader, SnapshotRuleRunner<BridgeSnapshot> rules) : IVersionRules
{
    /// <summary><c>bridge</c>.</summary>
    public string Kind => SnapshotKinds.Bridge;

    /// <summary>Runs every bridge rule on a stored version.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync(await loader.LoadVersionAsync(versionId, cancellationToken), cancellationToken);
    }
}
