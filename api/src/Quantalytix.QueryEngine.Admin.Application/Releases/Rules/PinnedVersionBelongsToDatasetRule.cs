using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>pinned_version_belongs_to_dataset</c>: every manifest row pins a version of its own dataset. The engine joins the manifest on
/// both ids, so a row whose version belongs to another dataset, or no longer exists, drops the dataset from queries.
/// </summary>
public sealed class PinnedVersionBelongsToDatasetRule : IReleaseRule
{
    /// <summary>Gate: a manifest edit cannot pin a version of another dataset.</summary>
    public RuleStage Stage => RuleStage.Gate;

    /// <summary>Reports every row whose dataset version is not one of its dataset's versions.</summary>
    /// <param name="snapshot">The release.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>One violation per offending row.</returns>
    public Task<IReadOnlyList<ReleaseViolation>> EvaluateAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        var owned = snapshot.Surroundings.DatasetVersions.Select(version => (version.DatasetId, version.DatasetVersionId)).ToHashSet();
        var keys = snapshot.Surroundings.Datasets.ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        IReadOnlyList<ReleaseViolation> violations =
        [
            .. snapshot.Manifest
                .Where(row => !owned.Contains((row.DatasetId, row.DatasetVersionId)))
                .Select(row => ReleaseFindings.OnRow(
                    "pinned_version_belongs_to_dataset",
                    $"Dataset '{keys[row.DatasetId]}' pins dataset version {row.DatasetVersionId}, which is not one of its versions.",
                    row.DatasetId))
        ];
        return Task.FromResult(violations);
    }
}
