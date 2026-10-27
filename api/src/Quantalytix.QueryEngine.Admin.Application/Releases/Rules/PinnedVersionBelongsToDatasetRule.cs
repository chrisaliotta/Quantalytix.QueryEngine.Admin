using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>pinned_version_belongs_to_dataset</c>: every manifest row pins a version of its own dataset. The engine joins the
/// manifest on both IDs, so a row whose version belongs to another dataset, or no longer exists, drops the dataset from queries.
/// </summary>
public sealed class PinnedVersionBelongsToDatasetRule(ReleaseManifestReader reader, ITableGateway<DatasetVersionRecord> datasetVersions)
    : IPublicationRule
{
    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every manifest row whose dataset version is not one of its dataset's versions.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per offending row.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var pinned = await reader.ListDatasetsAsync(versionId, cancellationToken);
        var versions = await datasetVersions.ListAsync(new { DatasetVersionId = pinned.Select(dataset => dataset.Row.DatasetVersionId).ToArray() }, cancellationToken);
        var owned = versions.Select(version => (version.DatasetId, version.DatasetVersionId)).ToHashSet();
        return
        [
            .. pinned
                .Where(dataset => !owned.Contains((dataset.Row.DatasetId, dataset.Row.DatasetVersionId)))
                .Select(dataset => new RuleViolation
                {
                    Code = "pinned_version_belongs_to_dataset",
                    Message = $"Dataset '{dataset.DatasetKey}' pins dataset version {dataset.Row.DatasetVersionId}, which is not one of its versions."
                })
        ];
    }
}
