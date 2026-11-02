using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Derives <c>qe_dimension_closure</c> from a version's member tree: every member write replaces it, and every version created with
/// members writes it. The closure is never edited on its own; the engine reads it to group members by level, depth or ancestor.
/// </summary>
public static class ClosureBuilder
{
    /// <summary>
    /// The closure of a member tree. Walks down from the roots, so every visited member's ancestors are exactly the path that
    /// reached it. Members of a cycle are never reached, because no root sits above them.
    /// </summary>
    /// <param name="versionId">The version the members belong to.</param>
    /// <param name="tree">The version's members.</param>
    /// <returns>One row per ancestor-descendant pair, each member its own ancestor at depth 0.</returns>
    public static List<DimensionClosureRecord> ClosureOf(long versionId, IReadOnlyList<DimensionMemberRecord> tree)
    {
        var children = tree.Where(member => member.ParentDimensionMemberId is not null).ToLookup(member => member.ParentDimensionMemberId!.Value, member => member.DimensionMemberId);
        var pending = new Queue<(long DimensionMemberId, long[] Ancestors)>(
            tree.Where(member => member.ParentDimensionMemberId is null).Select(member => (member.DimensionMemberId, Array.Empty<long>())));
        var rows = new List<DimensionClosureRecord>();
        while (pending.TryDequeue(out var node))
        {
            rows.Add(Row(versionId, node.DimensionMemberId, node.DimensionMemberId, 0));
            for (var index = 0; index < node.Ancestors.Length; index++)
                rows.Add(Row(versionId, node.Ancestors[index], node.DimensionMemberId, node.Ancestors.Length - index));
            long[] path = [.. node.Ancestors, node.DimensionMemberId];
            foreach (var child in children[node.DimensionMemberId]) pending.Enqueue((child, path));
        }

        return rows;
    }

    /// <summary>One closure row.</summary>
    private static DimensionClosureRecord Row(long versionId, long ancestorId, long descendantId, int depth)
    {
        return new DimensionClosureRecord
        {
            DimensionVersionId = versionId,
            AncestorDimensionMemberId = ancestorId,
            DescendantDimensionMemberId = descendantId,
            Depth = depth
        };
    }
}
