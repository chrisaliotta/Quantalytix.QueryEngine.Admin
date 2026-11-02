using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The dataset snapshot rules as the dataset kind's <see cref="IVersionRules"/>, so restore runs on a stored version the same rules
/// a change set's commit runs on its pending result.
/// </summary>
/// <param name="loader">Reads the version as a snapshot.</param>
/// <param name="rules">Runs every dataset rule.</param>
public sealed class DatasetVersionRules(DatasetSnapshotLoader loader, SnapshotRuleRunner<DatasetSnapshot> rules) : IVersionRules
{
    /// <summary><c>dataset</c>.</summary>
    public string Kind => SnapshotKinds.Dataset;

    /// <summary>Runs every dataset rule on a stored version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync(await loader.LoadAsync(versionId, cancellationToken), cancellationToken);
    }
}
