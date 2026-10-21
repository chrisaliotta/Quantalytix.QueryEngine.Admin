using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// A report version and the versions with its status of the objects its report owns: the report dimension's version, which holds
/// the rows, and each bridge's version, which holds the row mappings. Together they are one design.
/// </summary>
public sealed record ReportDesignScope
{
    /// <summary>The report.</summary>
    public required ReportRecord Report { get; init; }

    /// <summary>The report version.</summary>
    public required ReportVersionRecord Version { get; init; }

    /// <summary>The report dimension's version with the report version's status.</summary>
    public required DimensionVersionRecord Structure { get; init; }

    /// <summary>Every bridge into the report dimension, by bridge ID.</summary>
    public required IReadOnlyDictionary<long, BridgeRecord> Bridges { get; init; }

    /// <summary>The version with the report version's status of each bridge that has one, by bridge ID.</summary>
    public required IReadOnlyDictionary<long, BridgeVersionRecord> BridgeVersions { get; init; }
}
