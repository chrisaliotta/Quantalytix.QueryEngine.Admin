using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>pinned_version_published</c>: a release pins only published dataset versions. The engine reads a release's datasets through
/// <c>vw_qe_release_dataset</c>, which keeps published versions only, so any other pin would silently drop its dataset.
/// </summary>
public sealed class PinnedDatasetVersionPublishedRule : IReleaseRule
{
    /// <summary>Gate: a manifest edit cannot pin an unpublished version.</summary>
    public RuleStage Stage => RuleStage.Gate;

    /// <summary>Reports every row that pins a version of its dataset that is not published.</summary>
    /// <param name="snapshot">The release.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>One violation per unpublished pin.</returns>
    public Task<IReadOnlyList<ReleaseViolation>> EvaluateAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        var versions = snapshot.Surroundings.DatasetVersions.ToDictionary(version => version.DatasetVersionId);
        var keys = snapshot.Surroundings.Datasets.ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        IReadOnlyList<ReleaseViolation> violations =
        [
            .. snapshot.Manifest
                .Select(row => (Row: row, Version: versions.GetValueOrDefault(row.DatasetVersionId)))
                .Where(pin => pin.Version is { } version && version.DatasetId == pin.Row.DatasetId && version.Status != PublicationStatuses.Published)
                .Select(pin => ReleaseFindings.OnRow(
                    "pinned_version_published",
                    $"Dataset '{keys[pin.Row.DatasetId]}' pins v{pin.Version!.VersionNumber}, which is {pin.Version.Status}; a release pins only published versions.",
                    pin.Row.DatasetId))
        ];
        return Task.FromResult(violations);
    }
}
