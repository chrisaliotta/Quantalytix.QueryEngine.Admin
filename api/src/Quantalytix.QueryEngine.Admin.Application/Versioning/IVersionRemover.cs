namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Deletes one kind's versions with their content, and identities that no snapshot holds a version of any more, one statement per
/// table for all of them. Deleting a snapshot removes its versions; restore removes the live versions of identities the restored
/// snapshot lacks. Each versioned module implements one.
/// </summary>
public interface IVersionRemover
{
    /// <summary>The <c>SnapshotKind</c> the remover serves.</summary>
    string Kind { get; }

    /// <summary>Deletes versions and every content row they own.</summary>
    /// <param name="versionIds">The versions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    Task RemoveVersionsAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken);

    /// <summary>Deletes identity rows and the rows they own outside their versions; they hold no version.</summary>
    /// <param name="identityIds">The identities.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    Task RemoveIdentitiesAsync(IReadOnlyCollection<long> identityIds, CancellationToken cancellationToken);
}
