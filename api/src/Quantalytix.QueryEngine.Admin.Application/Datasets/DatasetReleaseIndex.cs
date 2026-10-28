using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Which semantic releases list datasets and pin their versions, and which release is active (D2).</summary>
/// <param name="pins">The <c>qe_release_dataset</c> rows read.</param>
/// <param name="releases">The releases those rows belong to.</param>
/// <param name="activeReleaseId">The tenant's active release, or null.</param>
public sealed class DatasetReleaseIndex(IReadOnlyList<ReleaseDatasetRecord> pins, IReadOnlyList<SemanticReleaseRecord> releases, long? activeReleaseId)
{
    /// <summary>The releases by id.</summary>
    private readonly Dictionary<long, SemanticReleaseRecord> _releases = releases.ToDictionary(release => release.SemanticReleaseId);

    /// <summary>The releases whose manifest lists a dataset.</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <returns>The releases, by key.</returns>
    public IReadOnlyList<SemanticReleaseRecord> Listing(long datasetId)
    {
        return Of(pins.Where(pin => pin.DatasetId == datasetId));
    }

    /// <summary>The releases that pin a version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <returns>The releases, by key.</returns>
    public IReadOnlyList<SemanticReleaseRecord> Pinning(long versionId)
    {
        return Of(pins.Where(pin => pin.DatasetVersionId == versionId));
    }

    /// <summary>The contract's pins of a version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <returns>Each pinning release, with whether it is active.</returns>
    public IReadOnlyList<DatasetReleasePin> PinsOf(long versionId)
    {
        return
        [
            .. Pinning(versionId).Select(release => new DatasetReleasePin
            {
                ReleaseId = Changes.TemporaryIds.PresentStored(release.SemanticReleaseId),
                ReleaseKey = release.ReleaseKey,
                IsActive = release.SemanticReleaseId == activeReleaseId
            })
        ];
    }

    /// <summary>The version of a dataset the active release pins, which the engine reads.</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <returns>The version, or null when no active release pins one.</returns>
    public long? ActiveVersionOf(long datasetId)
    {
        return pins.SingleOrDefault(pin => pin.DatasetId == datasetId && pin.SemanticReleaseId == activeReleaseId)?.DatasetVersionId;
    }

    /// <summary>The active release, when it pins a version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <returns>The release the engine reads the version through, or null.</returns>
    public SemanticReleaseRecord? ActivePinning(long versionId)
    {
        return Pinning(versionId).SingleOrDefault(release => release.SemanticReleaseId == activeReleaseId);
    }

    /// <summary>Whether the active release pins a version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <returns>True when the engine reads the version.</returns>
    public bool IsActive(long versionId)
    {
        return pins.Any(pin => pin.DatasetVersionId == versionId && pin.SemanticReleaseId == activeReleaseId);
    }

    /// <summary>The distinct releases of some pins, by key.</summary>
    private List<SemanticReleaseRecord> Of(IEnumerable<ReleaseDatasetRecord> rows)
    {
        return [.. rows.Select(pin => pin.SemanticReleaseId).Distinct().Select(id => _releases[id]).OrderBy(release => release.ReleaseKey, StringComparer.Ordinal)];
    }
}
