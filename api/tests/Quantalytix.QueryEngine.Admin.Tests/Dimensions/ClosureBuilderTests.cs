using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms that <c>qe_dimension_closure</c> is derived from the member tree: a depth-0 row per member and a row per
/// ancestor-descendant pair with the edge distance as depth; and that a parent cycle gets no rows. The
/// engine groups members by level, depth and ancestor through these rows, so they must match the parent links exactly.
/// </summary>
public sealed class ClosureBuilderTests
{
    /// <summary>The version the members belong to.</summary>
    private const long VersionId = 10;

    /// <summary>A three-level tree, US over CA and NY with LA under CA, gets every self row and every ancestor pair at its distance.</summary>
    [Fact]
    public void ThreeLevelTree_GetsSelfRowsAndEdgeDistances()
    {
        var closure = ClosureBuilder.ClosureOf(
            VersionId,
            [Member(1, VersionId, "US"), Member(2, VersionId, "CA", parentId: 1), Member(3, VersionId, "NY", parentId: 1), Member(4, VersionId, "LA", parentId: 2)]);

        (long Ancestor, long Descendant, int Depth)[] expected =
        [
            (1, 1, 0), (2, 2, 0), (3, 3, 0), (4, 4, 0),
            (1, 2, 1), (1, 3, 1), (2, 4, 1),
            (1, 4, 2)
        ];
        Assert.Equal(expected.Order(), PairsOf(closure));
        Assert.All(closure, row => Assert.Equal(VersionId, row.DimensionVersionId));
    }

    /// <summary>
    /// A parent cycle (A under B under A, with C under A) has no root, so the walk from the roots never reaches it and it gets no
    /// rows, while a separate rooted branch still gets its rows.
    /// </summary>
    [Fact]
    public void Cycle_GetsNoRows()
    {
        var closure = ClosureBuilder.ClosureOf(
            VersionId,
            [
                Member(1, VersionId, "A", parentId: 2),
                Member(2, VersionId, "B", parentId: 1),
                Member(3, VersionId, "C", parentId: 1),
                Member(4, VersionId, "R"),
                Member(5, VersionId, "S", parentId: 4)
            ]);

        (long Ancestor, long Descendant, int Depth)[] expected = [(4, 4, 0), (5, 5, 0), (4, 5, 1)];
        Assert.Equal(expected.Order(), PairsOf(closure));
    }

    /// <summary>The closure rows as sorted (ancestor, descendant, depth) triples.</summary>
    private static IEnumerable<(long Ancestor, long Descendant, int Depth)> PairsOf(IEnumerable<DimensionClosureRecord> closure)
    {
        return closure.Select(row => (row.AncestorDimensionMemberId, row.DescendantDimensionMemberId, row.Depth)).Order();
    }
}
