using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A semantic release as the release list shows it: the row, whether it is the active release, and how much it pins.</summary>
public sealed record ReleaseSummary
{
    /// <summary>The release row.</summary>
    public required SemanticReleaseRecord Release { get; init; }

    /// <summary>Whether <c>qe_database_info.active_release_id</c> points at the release, so runtime queries use it.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The number of datasets the release's manifest pins.</summary>
    public required int DatasetCount { get; init; }

    /// <summary>The number of reports the release pins.</summary>
    public required int ReportCount { get; init; }
}
