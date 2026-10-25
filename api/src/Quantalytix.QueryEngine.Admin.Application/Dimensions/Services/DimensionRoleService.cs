using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Reads, creates, edits and deletes the roles of dimensions: the named uses of a dimension that bindings, calculation dimensions,
/// bridge sources and report scenarios name, and whose keys requests use. Roles are not versioned, and a role's key and dimension
/// never change.
/// </summary>
public sealed class DimensionRoleService(
    ITableGateway<DimensionRoleRecord> roles,
    DimensionDeleteChecks deleteChecks,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>Lists the roles of a dimension.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The roles by key, ignoring case.</returns>
    public async Task<IReadOnlyList<DimensionRoleRecord>> ListAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var rows = await roles.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        return [.. rows.OrderBy(row => row.DimensionRoleKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Creates a role of a dimension. Nothing names a new role yet, so the query engine's caches stay valid.</summary>
    /// <param name="dimensionId">The dimension, the role's template.</param>
    /// <param name="input">The new role, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored role.</returns>
    public async Task<DimensionRoleRecord> CreateAsync(long dimensionId, NewDimensionRoleInput input, CancellationToken cancellationToken)
    {
        var role = new DimensionRoleRecord
        {
            DimensionRoleId = ids.NextId(),
            DimensionId = dimensionId,
            DimensionRoleKey = input.DimensionRoleKey,
            DisplayName = input.DisplayName,
            Description = input.Description
        };
        await roles.InsertAsync(role, cancellationToken);
        return role;
    }

    /// <summary>
    /// Edits a role's display name and description. A change advances the semantic revision, because the query engine's catalog
    /// shows both.
    /// </summary>
    /// <param name="dimensionRoleId">The role.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The stored role.</returns>
    public async Task<DimensionRoleRecord> UpdateAsync(long dimensionRoleId, DimensionRoleInput input, CancellationToken cancellationToken)
    {
        var current = await roles.GetAsync(new { DimensionRoleId = dimensionRoleId }, cancellationToken);
        var updated = current with { DisplayName = input.DisplayName, Description = input.Description };
        await roles.UpdateAsync(updated, cancellationToken);
        if (updated != current) await semanticRevision.AdvanceAsync(cancellationToken);
        return updated;
    }

    /// <summary>Deletes a role that no binding, calculation dimension, report source or report scenario names.</summary>
    /// <param name="dimensionRoleId">The role.</param>
    /// <param name="cancellationToken">Cancels the reads and the delete.</param>
    /// <returns>Null when the role was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long dimensionRoleId, CancellationToken cancellationToken)
    {
        var role = await roles.GetAsync(new { DimensionRoleId = dimensionRoleId }, cancellationToken);
        var conflict = await deleteChecks.RoleAsync(role, cancellationToken);
        if (conflict is not null) return conflict;
        await roles.DeleteAsync(new { DimensionRoleId = dimensionRoleId }, cancellationToken);
        return null;
    }
}
