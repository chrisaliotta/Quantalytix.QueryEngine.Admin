namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ReleaseDiff</c>: the manifest set difference going from one release to another (OI-38).</summary>
public sealed record ReleaseDiff
{
    /// <summary>The release compared from.</summary>
    public required long FromReleaseId { get; init; }

    /// <summary>The release compared to.</summary>
    public required long ToReleaseId { get; init; }

    /// <summary>Datasets only <c>to</c> pins.</summary>
    public required IReadOnlyList<ReleaseDiffEntry> Added { get; init; }

    /// <summary>Datasets only <c>from</c> pins, with the enabled reports that read them.</summary>
    public required IReadOnlyList<ReleaseDiffRemoval> Removed { get; init; }

    /// <summary>Datasets both pin at different versions.</summary>
    public required IReadOnlyList<ReleaseDiffChange> Changed { get; init; }

    /// <summary>Datasets pinned at the same version in both.</summary>
    public required int Unchanged { get; init; }
}

/// <summary>The contract's <c>ReleaseDiffEntry</c>: a dataset and the version one side pins.</summary>
public record ReleaseDiffEntry
{
    /// <summary>The dataset.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The pinned dataset version.</summary>
    public required long VersionId { get; init; }
}

/// <summary>The contract's <c>ReleaseDiffRemoval</c>: a dataset going away, with the enabled reports that read it.</summary>
public sealed record ReleaseDiffRemoval : ReleaseDiffEntry
{
    /// <summary>Keys of the enabled reports with a source on the dataset.</summary>
    public required IReadOnlyList<string> ReadBy { get; init; }
}

/// <summary>The contract's <c>ReleaseDiffChange</c>: a dataset pinned at another version.</summary>
public sealed record ReleaseDiffChange
{
    /// <summary>The dataset.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The version <c>from</c> pins.</summary>
    public required long FromVersionId { get; init; }

    /// <summary>The version <c>to</c> pins.</summary>
    public required long ToVersionId { get; init; }
}
