using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The dataset snapshot rules as the version workflow's one <c>dataset_version</c> publication rule, so validate and publish run on
/// the stored version the same rules a change set's preview runs on its pending result.
/// </summary>
/// <param name="loader">Reads the version as a snapshot.</param>
/// <param name="rules">Runs every dataset rule.</param>
public sealed class DatasetVersionRules(DatasetSnapshotLoader loader, DatasetRules rules) : IPublicationRule
{
    /// <summary><c>dataset_version</c>.</summary>
    public string Kind => VersionKinds.DatasetVersion;

    /// <summary>Runs every dataset rule on a stored version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync(await loader.LoadAsync(versionId, cancellationToken), cancellationToken);
    }
}
