using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Reads and edits the levels of a dimension version: optional business labels for the hierarchy's levels.</summary>
public sealed class DimensionLevelService(
    ITableGateway<DimensionLevelRecord> levels,
    DimensionDeleteChecks deleteChecks,
    IIdGenerator ids)
{
    /// <summary>Lists a version's levels.</summary>
    /// <param name="dimensionVersionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The levels by ordinal, then key.</returns>
    public async Task<IReadOnlyList<DimensionLevelRecord>> ListAsync(long dimensionVersionId, CancellationToken cancellationToken)
    {
        var rows = await levels.ListAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken);
        return [.. rows.OrderBy(row => row.Ordinal).ThenBy(row => row.LevelKey, StringComparer.Ordinal)];
    }

    /// <summary>Adds a level to a draft version.</summary>
    /// <param name="dimensionVersionId">The draft version.</param>
    /// <param name="input">The new level.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored level.</returns>
    public async Task<DimensionLevelRecord> CreateAsync(long dimensionVersionId, NewLevelInput input, CancellationToken cancellationToken)
    {
        var level = new DimensionLevelRecord
        {
            DimensionLevelId = ids.NextId(),
            DimensionVersionId = dimensionVersionId,
            LevelKey = input.LevelKey,
            DisplayName = input.DisplayName,
            Ordinal = input.Ordinal,
            Periodicity = input.Periodicity,
            IsLeafLevel = input.IsLeafLevel
        };
        await levels.InsertAsync(level, cancellationToken);
        return level;
    }

    /// <summary>Replaces a level's editable attributes; its key stays.</summary>
    /// <param name="reference">The draft version and its level.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored level.</returns>
    public async Task<DimensionLevelRecord> UpdateAsync(LevelReference reference, LevelInput input, CancellationToken cancellationToken)
    {
        var current = await levels.GetAsync(new { reference.DimensionLevelId }, cancellationToken);
        var updated = current with
        {
            DisplayName = input.DisplayName,
            Ordinal = input.Ordinal,
            Periodicity = input.Periodicity,
            IsLeafLevel = input.IsLeafLevel
        };
        await levels.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Deletes a level that no member uses.</summary>
    /// <param name="reference">The draft version and its level.</param>
    /// <param name="cancellationToken">Cancels the reads and the delete.</param>
    /// <returns>Null when the level was deleted; otherwise the detail naming the members that use it.</returns>
    public async Task<string?> DeleteAsync(LevelReference reference, CancellationToken cancellationToken)
    {
        var level = await levels.GetAsync(new { reference.DimensionLevelId }, cancellationToken);
        var conflict = await deleteChecks.LevelAsync(level, cancellationToken);
        if (conflict is not null) return conflict;
        await levels.DeleteAsync(new { reference.DimensionLevelId }, cancellationToken);
        return null;
    }
}
