using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Rebuilds <c>qe_dimension_closure</c> from the member tree after every member write, because a published version's edits are
/// live, and when a dimension version is validated, which covers a clone's copied members. The closure is derived from the
/// parent links and never edited on its own; the engine reads it to group members by level, depth or ancestor.
/// </summary>
public sealed class ClosureBuilder(
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<DimensionClosureRecord> closure) : IVersionLifecycleHandler
{
    /// <summary>The kind whose lifecycle the builder joins: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Rebuilds the version's closure before the publication rules run.</summary>
    /// <param name="versionId">The dimension version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the closure is rewritten.</returns>
    public Task OnValidatingAsync(long versionId, CancellationToken cancellationToken)
    {
        return RebuildAsync(versionId, cancellationToken);
    }

    /// <summary>
    /// Replaces the version's closure rows with one row per ancestor-descendant pair, including a depth-0 row per member.
    /// Only chains that end at a root count: a member in, or below, a parent cycle gets no rows, and the
    /// <c>hierarchy_acyclic</c> rule reports the cycle.
    /// </summary>
    /// <param name="versionId">The dimension version whose members changed.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the closure is rewritten.</returns>
    public async Task RebuildAsync(long versionId, CancellationToken cancellationToken)
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
