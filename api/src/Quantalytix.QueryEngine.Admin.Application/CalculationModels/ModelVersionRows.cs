using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Model version content stored in one table, whose rows the API returns as they are.</summary>
/// <typeparam name="TRecord">The content row.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
/// <param name="rows">The content table.</param>
/// <param name="ids">Allocates the IDs of new rows.</param>
public abstract class ModelVersionRows<TRecord, TInput>(ITableGateway<TRecord> rows, IIdGenerator ids) : IModelVersionContent<TRecord, TInput>
    where TRecord : class
{
    /// <summary>The content table, for the deletes a derived kind performs.</summary>
    protected ITableGateway<TRecord> Rows { get; } = rows;

    /// <summary>Lists the version's rows.</summary>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, by key.</returns>
    public async Task<IReadOnlyList<TRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var stored = await Rows.ListAsync(new { ModelVersionId = versionId }, cancellationToken);
        return [.. stored.OrderBy(KeyOf, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Whether a row belongs to the version.</summary>
    /// <param name="versionId">The model version the route names.</param>
    /// <param name="itemId">The row the route names.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the row is one of the version's.</returns>
    public async Task<bool> BelongsAsync(long versionId, long itemId, CancellationToken cancellationToken)
    {
        var matches = await Rows.ListAsync(Key(itemId), cancellationToken);
        return matches.Count == 1 && VersionOf(matches[0]) == versionId;
    }

    /// <summary>Adds a row to a draft version.</summary>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="input">The row, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored row.</returns>
    public async Task<TRecord> CreateAsync(long versionId, TInput input, CancellationToken cancellationToken)
    {
        var row = ToRecord(ids.NextId(), versionId, input);
        await Rows.InsertAsync(row, cancellationToken);
        return row;
    }

    /// <summary>Replaces a row of a draft version, keeping its ID.</summary>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="itemId">The row, one of the version's.</param>
    /// <param name="input">The row's new values, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored row.</returns>
    public async Task<TRecord> UpdateAsync(long versionId, long itemId, TInput input, CancellationToken cancellationToken)
    {
        var row = ToRecord(itemId, versionId, input);
        await Rows.UpdateAsync(row, cancellationToken);
        return row;
    }

    /// <summary>Deletes a row of a draft version with the rows that only it gives meaning to, unless a report uses it.</summary>
    /// <param name="itemId">The row, one of the version's.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the row was deleted; otherwise the detail naming what blocks the delete.</returns>
    public abstract Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken);

    /// <summary>The gateway key of a row, such as <c>new { VariableId = id }</c>.</summary>
    /// <param name="itemId">The row's ID.</param>
    /// <returns>An anonymous object holding the key property.</returns>
    protected abstract object Key(long itemId);

    /// <summary>The model version a row belongs to.</summary>
    /// <param name="row">The row.</param>
    /// <returns>Its model version ID.</returns>
    protected abstract long VersionOf(TRecord row);

    /// <summary>The key a row is listed by.</summary>
    /// <param name="row">The row.</param>
    /// <returns>Its key.</returns>
    protected abstract string KeyOf(TRecord row);

    /// <summary>The row an input describes.</summary>
    /// <param name="itemId">The row's ID: new for a create, the row's own for an edit.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected abstract TRecord ToRecord(long itemId, long versionId, TInput input);
}
