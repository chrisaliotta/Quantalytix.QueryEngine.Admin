using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ReleaseListItem</c>: a release with its activity and its difference from the active release.</summary>
public sealed record ReleaseListItem : Release
{
    /// <summary>A release row with what the list shows about it.</summary>
    /// <param name="row">The release row.</param>
    [SetsRequiredMembers]
    public ReleaseListItem(SemanticReleaseRecord row)
        : base(row)
    {
    }

    /// <summary>Whether runtime queries use the release.</summary>
    public required bool IsActive { get; init; }

    /// <summary>How many datasets the manifest pins.</summary>
    public required int DatasetCount { get; init; }

    /// <summary>What activating the release would change; null for the active release, or when no release is active.</summary>
    public required ReleaseDiff? DiffVsActive { get; init; }

    /// <summary>When the active release was last activated; null for every other release.</summary>
    public required DateTime? ActivatedAt { get; init; }

    /// <summary>Who last activated the active release; null for every other release.</summary>
    public required string? ActivatedBy { get; init; }
}
