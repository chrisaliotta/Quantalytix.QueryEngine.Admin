using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>A version store over the table gateway of the record that holds a kind's status and publication time.</summary>
/// <typeparam name="TRecord">The record of the versioned table.</typeparam>
/// <param name="gateway">The gateway of the versioned table.</param>
/// <param name="kind">The <c>version_kind</c> value the store serves.</param>
public abstract class VersionStore<TRecord>(ITableGateway<TRecord> gateway, string kind) : IVersionStore where TRecord : class
{
    /// <summary>The <c>version_kind</c> value this store serves.</summary>
    public string Kind { get; } = kind;

    /// <summary>The gateway of the versioned table.</summary>
    protected ITableGateway<TRecord> Gateway { get; } = gateway;

    /// <summary>Reads a version's state.</summary>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version's state.</returns>
    public async Task<VersionState> GetAsync(long id, CancellationToken cancellationToken)
    {
        var record = await Gateway.GetAsync(Key(id), cancellationToken);
        return ToState(record);
    }

    /// <summary>
    /// Writes a version's status, keeping every other column: publishing stamps the publication time, retiring the retirement
    /// time; any other status keeps the publication time.
    /// </summary>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="status">The new status.</param>
    /// <param name="changedAt">When the status changes (UTC).</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>A task that completes when the status is written.</returns>
    public async Task SetStatusAsync(long id, string status, DateTime changedAt, CancellationToken cancellationToken)
    {
        var record = await Gateway.GetAsync(Key(id), cancellationToken);
        var publishedAt = status == PublicationStatuses.Published ? changedAt : ToState(record).PublishedAt;
        DateTime? retiredAt = status == PublicationStatuses.Retired ? changedAt : null;
        await Gateway.UpdateAsync(WithStatus(record, status, publishedAt, retiredAt), cancellationToken);
    }

    /// <summary>The gateway key of a version, such as <c>new { DatasetVersionId = id }</c>.</summary>
    /// <param name="id">The version's surrogate key.</param>
    /// <returns>An anonymous object holding the key property.</returns>
    protected abstract object Key(long id);

    /// <summary>The state a record describes.</summary>
    /// <param name="record">The version's row.</param>
    /// <returns>The version's state.</returns>
    protected abstract VersionState ToState(TRecord record);

    /// <summary>A copy of a record with a new status, publication time and, where the table has one, retirement time.</summary>
    /// <param name="record">The version's row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <param name="retiredAt">The retirement time (UTC) to store; null unless the version retires.</param>
    /// <returns>The changed row.</returns>
    protected abstract TRecord WithStatus(TRecord record, string status, DateTime? publishedAt, DateTime? retiredAt);
}
