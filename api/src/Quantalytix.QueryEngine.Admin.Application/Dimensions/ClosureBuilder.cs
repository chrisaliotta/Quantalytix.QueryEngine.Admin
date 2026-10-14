using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Rebuilds <c>qe_dimension_closure</c> from the member tree when a dimension version is validated. The closure is derived
/// from the parent links and never edited on its own; the engine reads it to group members by level, depth or ancestor.
/// </summary>
public sealed class ClosureBuilder(
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<DimensionClosureRecord> closure) : IVersionLifecycleHandler
{
    /// <summary>The kind whose lifecycle the builder joins: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>
    /// Replaces the version's closure rows with one row per ancestor-descendant pair, including a depth-0 row per member.
    /// Only chains that end at a root count: a member in, or below, a parent cycle gets no rows, and the
    /// <c>hierarchy_acyclic</c> rule reports the cycle.
    /// </summary>
    /// <param name="versionId">The dimension version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the closure is rewritten.</returns>
    public async Task OnValidatingAsync(long versionId, CancellationToken cancellationToken)
    {
        var tree = await members.ListAsync(new { DimensionVersionId = versionId }, cancellationToken);
        await closure.DeleteAsync(new { DimensionVersionId = versionId }, cancellationToken);
        await closure.InsertManyAsync(ClosureOf(versionId, tree), cancellationToken);
    }

    /// <summary>Publishing changes no derived rows.</summary>
    /// <param name="versionId">The published dimension version.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task OnPublishedAsync(long versionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Walks down from the roots, so every visited member's ancestors are exactly the path that reached it. Members of a
    /// cycle are never reached, because no root sits above them.
    /// </summary>
    private static List<DimensionClosureRecord> ClosureOf(long versionId, IReadOnlyList<DimensionMemberRecord> tree)
    {
        var children = tree.Where(member => member.ParentMemberId is not null).ToLookup(member => member.ParentMemberId!.Value, member => member.MemberId);
        var pending = new Queue<(long MemberId, long[] Ancestors)>(
            tree.Where(member => member.ParentMemberId is null).Select(member => (member.MemberId, Array.Empty<long>())));
        var rows = new List<DimensionClosureRecord>();
        while (pending.TryDequeue(out var node))
        {
            rows.Add(Row(versionId, node.MemberId, node.MemberId, 0));
            for (var index = 0; index < node.Ancestors.Length; index++)
                rows.Add(Row(versionId, node.Ancestors[index], node.MemberId, node.Ancestors.Length - index));
            long[] path = [.. node.Ancestors, node.MemberId];
            foreach (var child in children[node.MemberId]) pending.Enqueue((child, path));
        }

        return rows;
    }

    /// <summary>One closure row.</summary>
    private static DimensionClosureRecord Row(long versionId, long ancestorId, long descendantId, int depth)
    {
        return new DimensionClosureRecord
        {
            DimensionVersionId = versionId,
            AncestorMemberId = ancestorId,
            DescendantMemberId = descendantId,
            Depth = depth
        };
    }
}
