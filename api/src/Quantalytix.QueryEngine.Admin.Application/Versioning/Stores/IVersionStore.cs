namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>Reads and changes the lifecycle state of one kind of versioned object.</summary>
public interface IVersionStore
{
    /// <summary>The <c>version_kind</c> value this store serves.</summary>
    string Kind { get; }

    /// <summary>Reads a version's state.</summary>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version's state.</returns>
    Task<VersionState> GetAsync(long id, CancellationToken cancellationToken);

    /// <summary>Writes a version's status and publication time.</summary>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the status is written.</returns>
    Task SetStatusAsync(long id, string status, DateTime? publishedAt, CancellationToken cancellationToken);
}
