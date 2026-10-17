namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Everything the release rules for report pins read: the pinned reports and the dataset versions the release pins.</summary>
public sealed record ReleaseReports
{
    /// <summary>The pinned reports, by report key.</summary>
    public required IReadOnlyList<PinnedReport> Reports { get; init; }

    /// <summary>The dataset versions the release pins, by dataset ID.</summary>
    public required IReadOnlyDictionary<long, PinnedDatasetVersion> Datasets { get; init; }

    /// <summary>The keys of every dataset a pinned report's sources read, by dataset ID, pinned or not.</summary>
    public required IReadOnlyDictionary<long, string> DatasetKeys { get; init; }

    /// <summary>The target dimension version and cardinality of every bridge version a pinned dataset version's bindings use.</summary>
    public required IReadOnlyDictionary<long, (long TargetDimensionVersionId, string Cardinality)> Bridges { get; init; }

    /// <summary>
    /// The dimension version a binding resolves members in: its bridge version's target for a bridge binding, otherwise its
    /// pinned version; null when the release leaves the version to the request time.
    /// </summary>
    /// <param name="binding">A binding of a pinned dataset version.</param>
    /// <returns>The pinned dimension version, or null.</returns>
    public long? SelectedVersion(Metadata.DimensionBindingRecord binding)
    {
        return binding.BridgeVersionId is { } bridge ? Bridges[bridge].TargetDimensionVersionId : binding.PinnedDimensionVersionId;
    }
}
