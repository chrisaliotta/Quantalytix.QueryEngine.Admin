namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// The version store of a living kind, whose identities each have at most one published version; publishing a version replaces
/// the published version of its identity.
/// </summary>
public interface ILivingVersionStore : IVersionStore
{
    /// <summary>The identity a version belongs to, such as its dimension.</summary>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The identity's surrogate key.</returns>
    Task<long> IdentityIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>The other published versions of the identity a version belongs to.</summary>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The sibling versions' surrogate keys; at most one, the version a publish replaces.</returns>
    Task<IReadOnlyList<long>> PublishedSiblingIdsAsync(long id, CancellationToken cancellationToken);

    /// <summary>Records why a frozen version was frozen.</summary>
    /// <param name="id">The frozen version's surrogate key.</param>
    /// <param name="note">The note, such as <c>FY26 close</c>, or null.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>A task that completes when the note is written.</returns>
    Task SetFreezeNoteAsync(long id, string? note, CancellationToken cancellationToken);
}
