namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ReleaseDetail</c>: a release page's read model, and the state of a manifest change set.</summary>
public sealed record ReleaseDetail
{
    /// <summary>The release.</summary>
    public required Release Release { get; init; }

    /// <summary>Whether runtime queries use the release.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The pinned dataset versions, by dataset.</summary>
    public required IReadOnlyList<ManifestEntry> Manifest { get; init; }

    /// <summary>Every activation of the release, newest first.</summary>
    public required IReadOnlyList<ReleaseActivation> Activations { get; init; }
}
