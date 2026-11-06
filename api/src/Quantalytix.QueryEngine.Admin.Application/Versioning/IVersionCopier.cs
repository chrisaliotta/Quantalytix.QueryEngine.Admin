namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Copies one kind's versions, their attributes and every content row, into a snapshot. Internal ids are remapped through one
/// <see cref="IdRemap"/>, so references between the copied rows point at the copies; references to identities keep their ids.
/// Capture copies every live version into a new frozen snapshot; restore copies frozen versions over their identities' live versions.
/// A batch writes each table with one statement, see <see cref="VersionCopyBatch"/>. The copier also keys the content it copies, so a
/// compare can tell a copy left untouched from one edited since. Each versioned module implements one.
/// </summary>
public interface IVersionCopier
{
    /// <summary>The <c>SnapshotKind</c> the copier serves.</summary>
    string Kind { get; }

    /// <summary>Copies versions into a snapshot.</summary>
    /// <param name="copies">The versions to copy, each with the version it replaces, if any.</param>
    /// <param name="targetSnapshotId">The snapshot the copies belong to.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copies' version ids, in the order of <paramref name="copies"/>.</returns>
    Task<IReadOnlyList<long>> CopyAsync(IReadOnlyList<VersionCopy> copies, long targetSnapshotId, CancellationToken cancellationToken);

    /// <summary>
    /// The <see cref="ContentKey"/> of a version: its settings and every content row it copies, with the ids a copy renews replaced
    /// by the keys they name.
    /// </summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Equal for two versions of an identity exactly when the engine reads the same content from either.</returns>
    Task<string> ContentKeyAsync(long versionId, CancellationToken cancellationToken);
}
