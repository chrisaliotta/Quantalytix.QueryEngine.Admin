using Quantalytix.QueryEngine.Admin.Application.Dimensions;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// What the report rules check reports against besides their own content, in the reports' snapshot: its dataset versions, and its
/// versions of the dimensions their sources' and calculation dimensions' roles template.
/// </summary>
public sealed record ReportScope
{
    /// <summary>The snapshot's dataset versions, by dataset ID; a dataset the snapshot does not hold is missing.</summary>
    public required IReadOnlyDictionary<long, HeldDatasetVersion> Datasets { get; init; }

    /// <summary>The keys of every dataset a report's sources read, by dataset ID, held or not.</summary>
    public required IReadOnlyDictionary<long, string> DatasetKeys { get; init; }

    /// <summary>The key of every role a report's sources or calculation dimensions name, by role ID.</summary>
    public required IReadOnlyDictionary<long, string> RoleKeys { get; init; }

    /// <summary>
    /// The snapshot version's keys of the dimension each source's or calculation dimension's role templates, by role ID; null when
    /// the snapshot holds no version of the dimension.
    /// </summary>
    public required IReadOnlyDictionary<long, DimensionKeys?> Templates { get; init; }
}
