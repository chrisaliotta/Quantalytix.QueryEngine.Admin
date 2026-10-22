using Quantalytix.QueryEngine.Admin.Application.Dimensions;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// What the report rules check reports against besides their own content: the dataset versions of the release they run against,
/// and the published versions of the dimensions their calculation dimensions' roles template.
/// </summary>
public sealed record ReportScope
{
    /// <summary>The dataset versions the release pins, by dataset ID; none when no release is active.</summary>
    public required IReadOnlyDictionary<long, PinnedDatasetVersion> Datasets { get; init; }

    /// <summary>The keys of every dataset a report's sources read, by dataset ID, pinned or not.</summary>
    public required IReadOnlyDictionary<long, string> DatasetKeys { get; init; }

    /// <summary>The key of every role a report's sources or calculation dimensions name, by role ID.</summary>
    public required IReadOnlyDictionary<long, string> RoleKeys { get; init; }

    /// <summary>
    /// The published version's keys of the dimension each calculation dimension's role templates, by role ID; null while the
    /// dimension has no published version.
    /// </summary>
    public required IReadOnlyDictionary<long, DimensionKeys?> Templates { get; init; }
}
