using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>pinned_version_published</c>: a release pins only published dataset versions. The engine reads a release's datasets
/// through <c>vw_qe_release_dataset</c>, which keeps published versions only, so any other pin would silently drop its dataset.
/// </summary>
public sealed class PinnedDatasetVersionPublishedRule(ReleaseManifestReader reader, ITableGateway<DatasetVersionRecord> datasetVersions)
    : IPublicationRule
{
    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every pinned dataset version that is not published.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unpublished pin.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var pinned = await reader.ListDatasetsAsync(versionId, cancellationToken);
        var versions = await datasetVersions.ListAsync(new { DatasetVersionId = pinned.Select(dataset => dataset.Row.DatasetVersionId).ToArray() }, cancellationToken);
        return
        [
            .. pinned
                .Join(versions, dataset => dataset.Row.DatasetVersionId, version => version.DatasetVersionId, (dataset, version) => (dataset.DatasetKey, Version: version))
                .Where(pin => pin.Version.Status != PublicationStatuses.Published)
                .Select(pin => new RuleViolation
                {
                    Rule = "pinned_version_published",
                    Message = $"Dataset '{pin.DatasetKey}' pins v{pin.Version.VersionNumber}, which is {pin.Version.Status}; a release pins only published versions."
                })
        ];
    }
}
