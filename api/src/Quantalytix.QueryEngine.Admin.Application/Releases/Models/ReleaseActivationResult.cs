using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The outcome of a request to activate a release.</summary>
public sealed record ReleaseActivationResult
{
    /// <summary>True when the release was published and is now active; false when it was not published and nothing changed.</summary>
    public required bool Activated { get; init; }

    /// <summary>The release the request named.</summary>
    public required SemanticReleaseRecord Release { get; init; }

    /// <summary>The tenant's database info after the request.</summary>
    public required DatabaseInfoRecord DatabaseInfo { get; init; }
}
