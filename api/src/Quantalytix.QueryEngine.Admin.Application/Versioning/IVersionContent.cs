namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// One kind of content row of a model or report version, such as a model's variables or a report's sources. The rows keep their
/// IDs across edits, because other rows of the version reference them by ID; writes need an editable version, which the API checks.
/// </summary>
/// <typeparam name="TItem">The row as the API returns it.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
public interface IVersionContent<TItem, TInput>
{
    /// <summary>Lists the version's rows.</summary>
    /// <param name="versionId">The owning version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, by key.</returns>
    Task<IReadOnlyList<TItem>> ListAsync(long versionId, CancellationToken cancellationToken);

    /// <summary>Whether a row belongs to the version, which a route naming both must hold.</summary>
    /// <param name="versionId">The version the route names.</param>
    /// <param name="itemId">The row the route names.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the row is one of the version's.</returns>
    Task<bool> BelongsAsync(long versionId, long itemId, CancellationToken cancellationToken);

    /// <summary>Adds a row to an editable version.</summary>
    /// <param name="versionId">The editable version.</param>
    /// <param name="input">The row, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored row.</returns>
    Task<TItem> CreateAsync(long versionId, TInput input, CancellationToken cancellationToken);

    /// <summary>Replaces a row of an editable version, keeping its ID.</summary>
    /// <param name="versionId">The editable version.</param>
    /// <param name="itemId">The row, one of the version's.</param>
    /// <param name="input">The row's new values, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored row.</returns>
    Task<TItem> UpdateAsync(long versionId, long itemId, TInput input, CancellationToken cancellationToken);

    /// <summary>Deletes a row of an editable version with the rows that only it gives meaning to, unless something live uses it.</summary>
    /// <param name="itemId">The row, one of the version's.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the row was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken);
}
