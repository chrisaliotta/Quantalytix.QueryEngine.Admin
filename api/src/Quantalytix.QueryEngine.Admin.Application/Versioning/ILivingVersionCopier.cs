namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Copies a living kind's version into its identity's next draft, content and attributes included; the freeze of
/// <see cref="LivingVersionFreezer"/> publishes the copy. Each living kind's module implements one, over its own cloner.
/// </summary>
public interface ILivingVersionCopier
{
    /// <summary>The living <c>version_kind</c> the copier serves.</summary>
    string Kind { get; }

    /// <summary>Creates the identity's next draft as a copy of a version.</summary>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft's surrogate key.</returns>
    Task<long> CopyAsync(long sourceVersionId, CancellationToken cancellationToken);
}
