using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms that validating a dimension version rebuilds <c>qe_dimension_closure</c> from the member tree: a depth-0 row
/// per member, a row per ancestor-descendant pair with the edge distance as depth, and nothing stale left behind; and that a
/// parent cycle neither hangs the builder nor gets rows. The engine groups members by level, depth and ancestor through
/// these rows, so they must match the parent links exactly.
/// </summary>
public sealed class ClosureBuilderTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 10;

    /// <summary>Another version, whose closure must stay untouched.</summary>
    private const long OtherVersionId = 20;

    /// <summary>
    /// A three-level tree, US over CA and NY with LA under CA, gets every self row and every ancestor pair at its distance,
    /// and replaces the version's stale rows.
    /// </summary>
    /// <returns>A task that completes when the closure is checked.</returns>
    [Fact]
    public async Task ThreeLevelTree_GetsSelfRowsAndEdgeDistances()
    {
        var members = new InMemoryTableGateway<DimensionMemberRecord>(
            Member(1, VersionId, "US"),
            Member(2, VersionId, "CA", parentId: 1),
            Member(3, VersionId, "NY", parentId: 1),
            Member(4, VersionId, "LA", parentId: 2));
        var closure = new InMemoryTableGateway<DimensionClosureRecord>(
            Row(VersionId, 3, 4, 1),
            Row(OtherVersionId, 7, 8, 1));

        await new ClosureBuilder(members, closure).OnValidatingAsync(VersionId, TestContext.Current.CancellationToken);

        (long Ancestor, long Descendant, int Depth)[] expected =
        [
            (1, 1, 0), (2, 2, 0), (3, 3, 0), (4, 4, 0),
            (1, 2, 1), (1, 3, 1), (2, 4, 1),
            (1, 4, 2)
        ];
        Assert.Equal(expected.Order(), PairsOf(closure, VersionId));
        Assert.Equal([Row(OtherVersionId, 7, 8, 1)], closure.All.Where(row => row.DimensionVersionId == OtherVersionId));
    }

    /// <summary>
    /// A parent cycle (A under B under A, with C under A) has no root, so the builder finishes and writes no rows for it,
    /// while a separate rooted branch still gets its rows.
    /// </summary>
    /// <returns>A task that completes when the closure is checked.</returns>
    [Fact(Timeout = 10_000)]
    public async Task Cycle_GetsNoRowsAndDoesNotHang()
    {
        var members = new InMemoryTableGateway<DimensionMemberRecord>(
            Member(1, VersionId, "A", parentId: 2),
            Member(2, VersionId, "B", parentId: 1),
            Member(3, VersionId, "C", parentId: 1),
            Member(4, VersionId, "R"),
            Member(5, VersionId, "S", parentId: 4));
        var closure = new InMemoryTableGateway<DimensionClosureRecord>();

        await new ClosureBuilder(members, closure).OnValidatingAsync(VersionId, TestContext.Current.CancellationToken);

        (long Ancestor, long Descendant, int Depth)[] expected = [(4, 4, 0), (5, 5, 0), (4, 5, 1)];
        Assert.Equal(expected.Order(), PairsOf(closure, VersionId));
    }

    /// <summary>The version's closure rows as sorted (ancestor, descendant, depth) triples.</summary>
    private static IEnumerable<(long Ancestor, long Descendant, int Depth)> PairsOf(InMemoryTableGateway<DimensionClosureRecord> closure, long versionId)
    {
        return closure.All
            .Where(row => row.DimensionVersionId == versionId)
            .Select(row => (row.AncestorMemberId, row.DescendantMemberId, row.Depth))
            .Order();
    }

    /// <summary>One closure row.</summary>
    private static DimensionClosureRecord Row(long versionId, long ancestorId, long descendantId, int depth)
    {
        return new DimensionClosureRecord { DimensionVersionId = versionId, AncestorMemberId = ancestorId, DescendantMemberId = descendantId, Depth = depth };
    }
}
