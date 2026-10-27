using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms <c>hierarchy_acyclic</c>: a tree passes, and each parent cycle is reported once, naming its members in parent
/// order, while members merely hanging below a cycle are not reported again. A cycle has no root, so its members would get
/// no closure rows and silently drop out of hierarchical grouping.
/// </summary>
public sealed class HierarchyAcyclicRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 10;

    /// <summary>A tree of three levels and a second root has no cycle.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task Tree_Passes()
    {
        var violations = await EvaluateAsync(
            Member(1, VersionId, "US"),
            Member(2, VersionId, "CA", parentId: 1),
            Member(3, VersionId, "LA", parentId: 2),
            Member(4, VersionId, "EU"));

        Assert.Empty(violations);
    }

    /// <summary>A, B and C parent each other in a loop; D hangs below A. One violation names the loop.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task Cycle_IsReportedOnce()
    {
        var violations = await EvaluateAsync(
            Member(1, VersionId, "A", parentId: 2),
            Member(2, VersionId, "B", parentId: 3),
            Member(3, VersionId, "C", parentId: 1),
            Member(4, VersionId, "D", parentId: 1));

        var violation = Assert.Single(violations);
        Assert.Equal("hierarchy_acyclic", violation.Code);
        Assert.Equal("Members 'A' -> 'B' -> 'C' -> 'A' form a parent cycle.", violation.Message);
    }

    /// <summary>A member that is its own parent is a cycle of one.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task OwnParent_IsACycle()
    {
        var violations = await EvaluateAsync(Member(1, VersionId, "X", parentId: 1));

        Assert.Equal(["Members 'X' -> 'X' form a parent cycle."], violations.Select(violation => violation.Message));
    }

    /// <summary>Runs the rule over the given members of the version.</summary>
    private static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params DimensionMemberRecord[] members)
    {
        var snapshot = Snapshot(1, VersionId, DimensionKinds.Hierarchical, null, members, []);
        return new HierarchyAcyclicRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);
    }
}
