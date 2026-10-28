using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Builds the release read models from stored or pending rows.</summary>
public static class ReleaseReadModels
{
    /// <summary>The release page's read model, which is also a manifest change set's state.</summary>
    /// <param name="snapshot">The release, stored or pending.</param>
    /// <returns>The detail.</returns>
    public static ReleaseDetail Detail(ReleaseSnapshot snapshot)
    {
        return new ReleaseDetail
        {
            Release = new Release(snapshot.Release),
            IsActive = snapshot.Release.SemanticReleaseId == snapshot.Surroundings.ActiveReleaseId,
            Manifest = [.. snapshot.Manifest.Select(row => new ManifestEntry { DatasetId = row.DatasetId, DatasetVersionId = row.DatasetVersionId })],
            Activations = [.. snapshot.Surroundings.Activations.Select(row => new ReleaseActivation(row))]
        };
    }

    /// <summary>The manifest set difference going from one release to another (OI-38), each list by dataset.</summary>
    /// <param name="fromReleaseId">The release compared from.</param>
    /// <param name="from">Its manifest.</param>
    /// <param name="toReleaseId">The release compared to.</param>
    /// <param name="to">Its manifest, stored or pending.</param>
    /// <param name="readBy">The keys of the enabled reports that read each dataset.</param>
    /// <returns>The difference.</returns>
    public static ReleaseDiff Diff(
        long fromReleaseId,
        IEnumerable<ReleaseDatasetRecord> from,
        long toReleaseId,
        IEnumerable<ReleaseDatasetRecord> to,
        ILookup<long, string> readBy)
    {
        var before = from.ToDictionary(row => row.DatasetId, row => row.DatasetVersionId);
        var after = to.ToDictionary(row => row.DatasetId, row => row.DatasetVersionId);
        return new ReleaseDiff
        {
            FromReleaseId = fromReleaseId,
            ToReleaseId = toReleaseId,
            Added =
            [
                .. after.Where(pin => !before.ContainsKey(pin.Key)).OrderBy(pin => pin.Key)
                    .Select(pin => new ReleaseDiffEntry { DatasetId = pin.Key, VersionId = pin.Value })
            ],
            Removed =
            [
                .. before.Where(pin => !after.ContainsKey(pin.Key)).OrderBy(pin => pin.Key)
                    .Select(pin => new ReleaseDiffRemoval { DatasetId = pin.Key, VersionId = pin.Value, ReadBy = [.. readBy[pin.Key]] })
            ],
            Changed =
            [
                .. after.Where(pin => before.TryGetValue(pin.Key, out var version) && version != pin.Value).OrderBy(pin => pin.Key)
                    .Select(pin => new ReleaseDiffChange { DatasetId = pin.Key, FromVersionId = before[pin.Key], ToVersionId = pin.Value })
            ],
            Unchanged = after.Count(pin => before.TryGetValue(pin.Key, out var version) && version == pin.Value)
        };
    }
}
