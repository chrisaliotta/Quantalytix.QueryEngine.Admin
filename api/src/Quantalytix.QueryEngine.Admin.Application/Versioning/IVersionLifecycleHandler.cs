namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Work a feature slice attaches to a version's lifecycle: deriving data before the publication rules run (for example,
/// rebuilding the closure table) and cascading after a publish.
/// </summary>
public interface IVersionLifecycleHandler
{
    /// <summary>The <c>version_kind</c> value the handler serves.</summary>
    string Kind { get; }

    /// <summary>Runs before the publication rules when a version leaves draft.</summary>
    /// <param name="versionId">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes when the derived data is written.</returns>
    Task OnValidatingAsync(long versionId, CancellationToken cancellationToken);

    /// <summary>Runs after a version's status became published.</summary>
    /// <param name="versionId">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes when the cascade is written.</returns>
    Task OnPublishedAsync(long versionId, CancellationToken cancellationToken);
}
