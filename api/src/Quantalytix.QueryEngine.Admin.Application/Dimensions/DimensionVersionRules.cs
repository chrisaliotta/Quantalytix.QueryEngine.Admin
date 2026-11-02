using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The dimension snapshot rules as the dimension kind's <see cref="IVersionRules"/>, which restore runs on a stored version.</summary>
/// <param name="loader">Reads the version as a snapshot.</param>
/// <param name="rules">Runs every dimension rule.</param>
public sealed class DimensionVersionRules(DimensionSnapshotLoader loader, SnapshotRuleRunner<DimensionSnapshot> rules) : IVersionRules
{
    /// <summary><c>dimension</c>.</summary>
    public string Kind => SnapshotKinds.Dimension;

    /// <summary>Runs every dimension rule on a stored version.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync(await loader.LoadVersionAsync(versionId, cancellationToken), cancellationToken);
    }
}
