namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A release's complete manifest, which replaces the current one.</summary>
public sealed record ReleaseManifestInput
{
    /// <summary>One row per dataset the release pins.</summary>
    public required IReadOnlyList<ReleaseDatasetInput> Datasets { get; init; }
}
