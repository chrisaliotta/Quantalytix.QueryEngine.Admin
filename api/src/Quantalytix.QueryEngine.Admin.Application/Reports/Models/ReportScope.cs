using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// What the report rules check together: report versions, and the dataset versions of the release they run against.
/// </summary>
public sealed record ReportScope
{
    /// <summary>The report versions, by report key.</summary>
    public required IReadOnlyList<ReportGraph> Reports { get; init; }

    /// <summary>The dataset versions the release pins, by dataset ID; none when no release is active.</summary>
    public required IReadOnlyDictionary<long, PinnedDatasetVersion> Datasets { get; init; }

    /// <summary>The keys of every dataset a report's sources read, by dataset ID, pinned or not.</summary>
    public required IReadOnlyDictionary<long, string> DatasetKeys { get; init; }

    /// <summary>Every bridge a report's sources and routes classify through, by bridge ID.</summary>
    public required IReadOnlyDictionary<long, BridgeRecord> Bridges { get; init; }

    /// <summary>The cardinality of the published version of every bridge a report's routes classify through, by bridge ID.</summary>
    public required IReadOnlyDictionary<long, string> BridgeCardinalities { get; init; }
}
