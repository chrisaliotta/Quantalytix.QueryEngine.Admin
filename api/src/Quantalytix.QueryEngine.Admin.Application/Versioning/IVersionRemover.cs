namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Deletes one kind's versions with their content, and an identity that no snapshot holds a version of any more. Deleting a snapshot
/// removes its versions; restore removes the live version of an identity the restored snapshot lacks. Each versioned module
/// implements one.
/// </summary>
public interface IVersionRemover
{
    /// <summary>The <c>SnapshotKind</c> the remover serves.</summary>
    string Kind { get; }

    /// <summary>Deletes a version and every content row it owns.</summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    Task RemoveVersionAsync(long versionId, CancellationToken cancellationToken);

    /// <summary>Deletes an identity row and the rows it owns outside its versions; it holds no version.</summary>
    /// <param name="identityId">The identity.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    Task RemoveIdentityAsync(long identityId, CancellationToken cancellationToken);
}
