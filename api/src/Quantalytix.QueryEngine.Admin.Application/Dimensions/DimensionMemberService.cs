using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Reads and edits the members of a dimension version: the nodes of its authoritative member tree. Every write rebuilds the
/// version's closure, so an edit to the published version reaches queries with its hierarchy intact, and an edit to the published
/// version that would leave a key some live object needs unresolved is refused before anything is written.
/// </summary>
public sealed class DimensionMemberService(
    ITableGateway<DimensionMemberRecord> members,
    DimensionDeleteChecks deleteChecks,
    DimensionChanges changes,
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

    /// <summary>Reads one member.</summary>
    /// <param name="reference">The version and its member.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The member.</returns>
    public Task<DimensionMemberRecord> GetAsync(MemberReference reference, CancellationToken cancellationToken)
    {
        return members.GetAsync(new { reference.MemberId }, cancellationToken);
    }

    /// <summary>Adds a member to a draft or published version; a new member leaves every key it found resolved.</summary>
    /// <param name="dimensionVersionId">The draft or published version.</param>
    /// <param name="input">The new member.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored member.</returns>
    public async Task<DimensionMemberRecord> CreateAsync(long dimensionVersionId, NewMemberInput input, CancellationToken cancellationToken)
    {
        var member = new DimensionMemberRecord
        {
            MemberId = ids.NextId(),
            DimensionVersionId = dimensionVersionId,
            MemberKey = input.MemberKey,
            DisplayName = input.DisplayName,
            Description = input.Description,
            ParentMemberId = input.ParentMemberId,
            LevelId = input.LevelId,
            IsAssignable = input.IsAssignable,
            SortOrder = input.SortOrder,
            Properties = input.Properties
        };
        await members.InsertAsync(member, cancellationToken);
        await closure.RebuildAsync(dimensionVersionId, cancellationToken);
        return member;
    }

    /// <summary>
    /// Replaces a member's editable attributes; its key stays. In the published version, an edit that would leave a key some
    /// live object needs unresolved, such as making a scenario a default comparison reads unassignable, is refused.
    /// </summary>
    /// <param name="reference">The draft or published version and its member.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The violations that kept the edit from being written; none when it was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> UpdateAsync(MemberReference reference, MemberInput input, CancellationToken cancellationToken)
    {
        var updated = await GetAsync(reference, cancellationToken) with
        {
            DisplayName = input.DisplayName,
            Description = input.Description,
            ParentMemberId = input.ParentMemberId,
            LevelId = input.LevelId,
            IsAssignable = input.IsAssignable,
            SortOrder = input.SortOrder,
            Properties = input.Properties
        };
        var violations = await changes.CheckAsync(
            reference.DimensionVersionId, stored => [.. stored.Select(member => member.MemberId == updated.MemberId ? updated : member)], cancellationToken);
        if (violations.Count > 0) return violations;

        await members.UpdateAsync(updated, cancellationToken);
        await closure.RebuildAsync(reference.DimensionVersionId, cancellationToken);
        return [];
    }

    /// <summary>
    /// Stores a drag-and-drop reparent or reorder: one update per member whose parent or sort order changed. Every placement
    /// is resolved against the version's members before anything is written, and in the published version a move that would leave
    /// a key some live object needs unresolved, such as taking the last assignable member out of a folder a default comparison
    /// reads the latest version of, is refused.
    /// </summary>
    /// <param name="dimensionVersionId">The draft or published version.</param>
    /// <param name="input">The new placements of the version's members.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The violations that kept the move from being written; none when it was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> ReorderAsync(long dimensionVersionId, MemberOrderInput input, CancellationToken cancellationToken)
    {
        var stored = (await members.ListAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken)).ToDictionary(member => member.MemberId);
        var changed = input.Items
            .Select(item => stored[item.MemberId] with { ParentMemberId = item.ParentMemberId, SortOrder = item.SortOrder })
            .Where(member => member != stored[member.MemberId])
            .ToDictionary(member => member.MemberId);
        var violations = await changes.CheckAsync(
            dimensionVersionId, current => [.. current.Select(member => changed.GetValueOrDefault(member.MemberId, member))], cancellationToken);
        if (violations.Count > 0) return violations;

        foreach (var member in changed.Values) await members.UpdateAsync(member, cancellationToken);
        await closure.RebuildAsync(dimensionVersionId, cancellationToken);
        return [];
    }

    /// <summary>
    /// Deletes a member that has no children and that no mapping entry, range band, bridge mapping or other live object needs.
    /// </summary>
    /// <param name="reference">The draft or published version and its member.</param>
    /// <param name="cancellationToken">Cancels the reads and the delete.</param>
    /// <returns>Null when the member was deleted; otherwise the detail naming what blocks the delete.</returns>
    public async Task<string?> DeleteAsync(MemberReference reference, CancellationToken cancellationToken)
    {
        var member = await GetAsync(reference, cancellationToken);
        var conflict = await deleteChecks.MemberAsync(member, cancellationToken);
        if (conflict is not null) return conflict;
        await members.DeleteAsync(new { reference.MemberId }, cancellationToken);
        await closure.RebuildAsync(reference.DimensionVersionId, cancellationToken);
        return null;
    }
}
