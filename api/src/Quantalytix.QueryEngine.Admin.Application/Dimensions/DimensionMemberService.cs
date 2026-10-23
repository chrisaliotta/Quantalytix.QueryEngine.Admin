using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Reads and edits the members of a dimension version: the nodes of its authoritative member tree. Every write rebuilds the
/// version's closure, so an edit to the published version reaches queries with its hierarchy intact.
/// </summary>
public sealed class DimensionMemberService(
    ITableGateway<DimensionMemberRecord> members,
    DimensionDeleteChecks deleteChecks,
    ClosureBuilder closure,
    IIdGenerator ids)
{
    /// <summary>Lists a version's members.</summary>
    /// <param name="dimensionVersionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The members by sort order, then key ignoring case.</returns>
    public async Task<IReadOnlyList<DimensionMemberRecord>> ListAsync(long dimensionVersionId, CancellationToken cancellationToken)
    {
        var rows = await members.ListAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken);
        return [.. rows.OrderBy(row => row.SortOrder).ThenBy(row => row.MemberKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Adds a member to a draft or published version.</summary>
    /// <param name="dimensionVersionId">The draft or published version.</param>
    /// <param name="input">The new member.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored member.</returns>
    public async Task<DimensionMemberRecord> CreateAsync(long dimensionVersionId, NewMemberInput input, CancellationToken cancellationToken)
    {
        var member = new DimensionMemberRecord
        {
            DimensionMemberId = ids.NextId(),
            DimensionVersionId = dimensionVersionId,
            MemberKey = input.MemberKey,
            DisplayName = input.DisplayName,
            Description = input.Description,
            ParentDimensionMemberId = input.ParentDimensionMemberId,
            DimensionLevelId = input.DimensionLevelId,
            IsAssignable = input.IsAssignable,
            SortOrder = input.SortOrder,
            Properties = input.Properties
        };
        await members.InsertAsync(member, cancellationToken);
        await closure.RebuildAsync(dimensionVersionId, cancellationToken);
        return member;
    }

    /// <summary>Replaces a member's editable attributes; its key stays.</summary>
    /// <param name="reference">The draft or published version and its member.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored member.</returns>
    public async Task<DimensionMemberRecord> UpdateAsync(MemberReference reference, MemberInput input, CancellationToken cancellationToken)
    {
        var current = await members.GetAsync(new { reference.DimensionMemberId }, cancellationToken);
        var updated = current with
        {
            DisplayName = input.DisplayName,
            Description = input.Description,
            ParentDimensionMemberId = input.ParentDimensionMemberId,
            DimensionLevelId = input.DimensionLevelId,
            IsAssignable = input.IsAssignable,
            SortOrder = input.SortOrder,
            Properties = input.Properties
        };
        await members.UpdateAsync(updated, cancellationToken);
        await closure.RebuildAsync(reference.DimensionVersionId, cancellationToken);
        return updated;
    }

    /// <summary>
    /// Stores a drag-and-drop reparent or reorder: one update per member whose parent or sort order changed. Every placement
    /// is resolved against the version's members before anything is written.
    /// </summary>
    /// <param name="dimensionVersionId">The draft or published version.</param>
    /// <param name="input">The new placements of the version's members.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The version's members after the change, by sort order, then key.</returns>
    public async Task<IReadOnlyList<DimensionMemberRecord>> ReorderAsync(long dimensionVersionId, MemberOrderInput input, CancellationToken cancellationToken)
    {
        var stored = (await members.ListAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken)).ToDictionary(member => member.DimensionMemberId);
        var changed = input.Items
            .Select(item => stored[item.DimensionMemberId] with { ParentDimensionMemberId = item.ParentDimensionMemberId, SortOrder = item.SortOrder })
            .Where(member => member != stored[member.DimensionMemberId])
            .ToList();
        foreach (var member in changed) await members.UpdateAsync(member, cancellationToken);
        await closure.RebuildAsync(dimensionVersionId, cancellationToken);
        return await ListAsync(dimensionVersionId, cancellationToken);
    }

    /// <summary>Deletes a member that has no children and that no mapping entry, range band or bridge mapping references.</summary>
    /// <param name="reference">The draft or published version and its member.</param>
    /// <param name="cancellationToken">Cancels the reads and the delete.</param>
    /// <returns>Null when the member was deleted; otherwise the detail naming what blocks the delete.</returns>
    public async Task<string?> DeleteAsync(MemberReference reference, CancellationToken cancellationToken)
    {
        var member = await members.GetAsync(new { reference.DimensionMemberId }, cancellationToken);
        var conflict = await deleteChecks.MemberAsync(member, cancellationToken);
        if (conflict is not null) return conflict;
        await members.DeleteAsync(new { reference.DimensionMemberId }, cancellationToken);
        await closure.RebuildAsync(reference.DimensionVersionId, cancellationToken);
        return null;
    }
}
