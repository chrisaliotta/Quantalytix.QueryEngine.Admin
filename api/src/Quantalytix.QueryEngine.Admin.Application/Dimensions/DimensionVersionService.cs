using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Reads, creates, clones, edits and deletes the versions of a dimension.</summary>
public sealed class DimensionVersionService(
    ITableGateway<DimensionVersionRecord> versions,
    DimensionVersionCloner cloner,
    DimensionVersionRemover remover,
    DimensionDeleteChecks deleteChecks,
    IIdGenerator ids)
{
    /// <summary>Lists a dimension's versions with their validity windows.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The versions by version number.</returns>
    public async Task<IReadOnlyList<DimensionVersionRecord>> ListAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var rows = await versions.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        return [.. rows.OrderBy(row => row.VersionNumber)];
    }

    /// <summary>Reads one version.</summary>
    /// <param name="dimensionVersionId">The version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version.</returns>
    public Task<DimensionVersionRecord> GetAsync(long dimensionVersionId, CancellationToken cancellationToken)
    {
        return versions.GetAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken);
    }

    /// <summary>
    /// Creates a draft with the dimension's next version number: empty, or a clone of <see cref="NewDimensionVersionInput.CloneFromVersionId"/>,
    /// which also copies the source's range data type unless the input supplies one.
    /// </summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="input">The new version's attributes and optional clone source.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft version.</returns>
    public async Task<DimensionVersionRecord> CreateAsync(long dimensionId, NewDimensionVersionInput input, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        var rangeDataType = input.RangeDataType;
        if (input.CloneFromVersionId is { } sourceId && rangeDataType is null)
            rangeDataType = (await GetAsync(sourceId, cancellationToken)).RangeDataType;

        var version = new DimensionVersionRecord
        {
            DimensionVersionId = ids.NextId(),
            DimensionId = dimensionId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            Status = PublicationStatuses.Draft,
            RangeDataType = rangeDataType,
            ValidFrom = input.ValidFrom,
            ValidTo = input.ValidTo
        };
        await versions.InsertAsync(version, cancellationToken);
        if (input.CloneFromVersionId is { } cloneFromVersionId) await cloner.CloneAsync(cloneFromVersionId, version, cancellationToken);
        return version;
    }

    /// <summary>Replaces a draft version's range data type and validity window.</summary>
    /// <param name="dimensionVersionId">The draft version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored version.</returns>
    public async Task<DimensionVersionRecord> UpdateAsync(long dimensionVersionId, DimensionVersionInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(dimensionVersionId, cancellationToken);
        var updated = current with { RangeDataType = input.RangeDataType, ValidFrom = input.ValidFrom, ValidTo = input.ValidTo };
        await versions.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a draft version with its content, unless another feature's rows pin it; draft mapping versions pinned to it go
    /// with it.
    /// </summary>
    /// <param name="dimensionVersionId">The draft version.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the version was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long dimensionVersionId, CancellationToken cancellationToken)
    {
        var version = await GetAsync(dimensionVersionId, cancellationToken);
        var conflict = await deleteChecks.VersionAsync(version, cancellationToken);
        if (conflict is not null) return conflict;
        await remover.RemoveAsync([dimensionVersionId], cancellationToken);
        return null;
    }
}
