using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Reads, creates, edits and deletes canonical dimensions: the identity rows that own the dimension versions.</summary>
public sealed class DimensionService(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> versions,
    DimensionVersionService dimensionVersions,
    DimensionVersionRemover remover,
    DimensionDeleteChecks deleteChecks,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>Lists every dimension.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The dimensions by key, ignoring case.</returns>
    public async Task<IReadOnlyList<DimensionRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await dimensions.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.DimensionKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one dimension.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The dimension.</returns>
    public Task<DimensionRecord> GetAsync(long dimensionId, CancellationToken cancellationToken)
    {
        return dimensions.GetAsync(new { DimensionId = dimensionId }, cancellationToken);
    }

    /// <summary>Creates a user-defined dimension and its draft version 1.</summary>
    /// <param name="input">The new dimension, already validated.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored dimension.</returns>
    public async Task<DimensionRecord> CreateAsync(NewDimensionInput input, CancellationToken cancellationToken)
    {
        var dimension = new DimensionRecord
        {
            DimensionId = ids.NextId(),
            DimensionKey = input.DimensionKey,
            DisplayName = input.DisplayName,
            DimensionKind = input.DimensionKind,
            DefinitionOrigin = DefinitionOrigins.User,
            IsRemovable = input.IsRemovable,
            IsEnabled = input.IsEnabled
        };
        await dimensions.InsertAsync(dimension, cancellationToken);
        var firstVersion = new NewDimensionVersionInput { RangeDataType = input.RangeDataType, ValidFrom = input.ValidFrom };
        await dimensionVersions.CreateAsync(dimension.DimensionId, firstVersion, cancellationToken);
        return dimension;
    }

    /// <summary>
    /// Edits a dimension's identity attributes. A <c>system</c> dimension keeps its stored <c>is_removable</c>, because platform
    /// fixtures must never become deletable. A changed display name or availability advances the semantic revision, because
    /// the query engine caches both.
    /// </summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The stored dimension.</returns>
    public async Task<DimensionRecord> UpdateAsync(long dimensionId, DimensionInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(dimensionId, cancellationToken);
        var updated = current with
        {
            DisplayName = input.DisplayName,
            IsEnabled = input.IsEnabled,
            IsRemovable = RemovabilityAfter(current, input)
        };
        await dimensions.UpdateAsync(updated, cancellationToken);
        if (updated.DisplayName != current.DisplayName || updated.IsEnabled != current.IsEnabled) await semanticRevision.AdvanceAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a removable dimension whose versions are all drafts and that nothing references: its drafts with their
    /// content first, then the dimension.
    /// </summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the dimension was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var dimension = await GetAsync(dimensionId, cancellationToken);
        var ownVersions = await versions.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        var conflict = await deleteChecks.DimensionAsync(dimension, ownVersions, cancellationToken);
        if (conflict is not null) return conflict;
        await remover.RemoveAsync([.. ownVersions.Select(version => version.DimensionVersionId)], cancellationToken);
        await dimensions.DeleteAsync(new { DimensionId = dimensionId }, cancellationToken);
        return null;
    }

    /// <summary>The removability an update stores: the input's, except that a system dimension keeps its own.</summary>
    private static bool RemovabilityAfter(DimensionRecord current, DimensionInput input)
    {
        if (current.DefinitionOrigin == DefinitionOrigins.System) return current.IsRemovable;
        return input.IsRemovable;
    }
}
