namespace Quantalytix.QueryEngine.Admin.Application.Content;

/// <summary>
/// One kind of content row an owner holds: the variables and calculated measures of a calculation model, the source profiles and
/// calculation dimensions of a report. A write composes the row an input describes before storing it, so the owner can check what
/// the write would leave before anything is written.
/// </summary>
/// <typeparam name="TItem">The content row as the API returns it.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
public interface IOwnedContent<TItem, TInput>
{
    /// <summary>Lists the owner's rows.</summary>
    /// <param name="ownerId">The owning model or report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, by key.</returns>
    Task<IReadOnlyList<TItem>> ListAsync(long ownerId, CancellationToken cancellationToken);

    /// <summary>Whether a row belongs to the owner.</summary>
    /// <param name="ownerId">The owner the route names.</param>
    /// <param name="itemId">The row the route names.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the row is one of the owner's.</returns>
    Task<bool> BelongsAsync(long ownerId, long itemId, CancellationToken cancellationToken);

    /// <summary>The row an input describes, under a new ID or the edited row's own.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="itemId">The edited row; null for a new row.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    TItem Compose(long ownerId, long? itemId, TInput input);

    /// <summary>Stores a new row.</summary>
    /// <param name="item">A row <see cref="Compose"/> made for a new row.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the row is stored.</returns>
    Task InsertAsync(TItem item, CancellationToken cancellationToken);

    /// <summary>Replaces a stored row, keeping its ID.</summary>
    /// <param name="item">A row <see cref="Compose"/> made for an edited row.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the row is stored.</returns>
    Task UpdateAsync(TItem item, CancellationToken cancellationToken);

    /// <summary>Deletes a row with the rows that only it gives meaning to, unless something live uses it.</summary>
    /// <param name="itemId">The row, one of the owner's.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the row was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken);
}
