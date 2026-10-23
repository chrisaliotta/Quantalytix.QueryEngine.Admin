using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Content;

/// <summary>Owned content stored in one table, whose rows the API returns as they are.</summary>
/// <typeparam name="TRecord">The content row.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
/// <param name="rows">The content table.</param>
/// <param name="ids">Allocates the IDs of new rows.</param>
public abstract class OwnedRows<TRecord, TInput>(ITableGateway<TRecord> rows, IIdGenerator ids) : IOwnedContent<TRecord, TInput>
    where TRecord : class
{
    /// <summary>The content table, for the deletes a derived kind performs.</summary>
    protected ITableGateway<TRecord> Rows { get; } = rows;

    /// <summary>Lists the owner's rows.</summary>
    /// <param name="ownerId">The owning model or report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, by key.</returns>
    public async Task<IReadOnlyList<TRecord>> ListAsync(long ownerId, CancellationToken cancellationToken)
    {
        var stored = await Rows.ListAsync(Owner(ownerId), cancellationToken);
        return [.. stored.OrderBy(KeyOf, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Whether a row belongs to the owner.</summary>
    /// <param name="ownerId">The owner the route names.</param>
    /// <param name="itemId">The row the route names.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the row is one of the owner's.</returns>
    public async Task<bool> BelongsAsync(long ownerId, long itemId, CancellationToken cancellationToken)
    {
        var matches = await Rows.ListAsync(Key(itemId), cancellationToken);
        return matches.Count == 1 && OwnerOf(matches[0]) == ownerId;
    }

    /// <summary>The row an input describes, under a new ID or the edited row's own.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="itemId">The edited row; null for a new row.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    public TRecord Compose(long ownerId, long? itemId, TInput input)
    {
        return ToRecord(itemId ?? ids.NextId(), ownerId, input);
    }

    /// <summary>Stores a new row.</summary>
    /// <param name="item">The composed row.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the row is stored.</returns>
    public Task InsertAsync(TRecord item, CancellationToken cancellationToken)
    {
        return Rows.InsertAsync(item, cancellationToken);
    }

    /// <summary>Replaces a stored row, keeping its ID.</summary>
    /// <param name="item">The composed row.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the row is stored.</returns>
    public Task UpdateAsync(TRecord item, CancellationToken cancellationToken)
    {
        return Rows.UpdateAsync(item, cancellationToken);
    }

    /// <summary>Deletes a row with the rows that only it gives meaning to, unless something live uses it.</summary>
    /// <param name="itemId">The row, one of the owner's.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the row was deleted; otherwise the detail naming what blocks the delete.</returns>
    public abstract Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken);

    /// <summary>The gateway filter of an owner's rows, such as <c>new { CalculationModelId = id }</c>.</summary>
    /// <param name="ownerId">The owner's ID.</param>
    /// <returns>An anonymous object holding the owner property.</returns>
    protected abstract object Owner(long ownerId);

    /// <summary>The gateway key of a row, such as <c>new { VariableId = id }</c>.</summary>
    /// <param name="itemId">The row's ID.</param>
    /// <returns>An anonymous object holding the key property.</returns>
    protected abstract object Key(long itemId);

    /// <summary>The owner a row belongs to.</summary>
    /// <param name="row">The row.</param>
    /// <returns>Its owner's ID.</returns>
    protected abstract long OwnerOf(TRecord row);

    /// <summary>The key a row is listed by.</summary>
    /// <param name="row">The row.</param>
    /// <returns>Its key.</returns>
    protected abstract string KeyOf(TRecord row);

    /// <summary>The row an input describes.</summary>
    /// <param name="itemId">The row's ID.</param>
    /// <param name="ownerId">The owner.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected abstract TRecord ToRecord(long itemId, long ownerId, TInput input);
}
