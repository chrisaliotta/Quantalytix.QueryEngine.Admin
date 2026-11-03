namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Copies one kind's version, its attributes and every content row, into a snapshot. Internal ids are remapped through one
/// <see cref="IdRemap"/>, so references between the copied rows point at the copies; references to identities keep their ids.
/// Capture copies every live version into a new frozen snapshot; restore copies a frozen version over its identity's live version.
/// The copier also keys the content it copies, so a compare can tell a copy left untouched from one edited since. Each versioned
/// module implements one.
/// </summary>
public interface IVersionCopier
{
    /// <summary>The <c>SnapshotKind</c> the copier serves.</summary>
    string Kind { get; }

    /// <summary>Copies a version into a snapshot.</summary>
    /// <param name="versionId">The version to copy.</param>
    /// <param name="targetSnapshotId">The snapshot the copy belongs to.</param>
    /// <param name="targetVersionId">
    /// A version of the same identity whose content the copy replaces in place, keeping its id; null to insert a new version.
    /// </param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copy's version id.</returns>
    Task<long> CopyAsync(long versionId, long targetSnapshotId, long? targetVersionId, CancellationToken cancellationToken);

    /// <summary>
    /// The <see cref="ContentKey"/> of a version: its settings and every content row it copies, with the ids a copy renews replaced
    /// by the keys they name.
    /// </summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Equal for two versions of an identity exactly when the engine reads the same content from either.</returns>
    Task<string> ContentKeyAsync(long versionId, CancellationToken cancellationToken);
}
