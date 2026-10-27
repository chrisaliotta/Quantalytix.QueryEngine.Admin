using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms that <c>bridge_binding_route</c> accepts a bridge binding whose source binding is in the same version and whose
/// bridge leads from the source binding's dimension to the bridge binding's dimension, and reports each break: a source
/// binding of another version, a bridge starting or ending at another dimension, and source bindings that lead back to the
/// bridge binding. The query engine follows this route to remap canonical members, and follows a cycle forever.
/// </summary>
public sealed class BridgeBindingRouteRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The branch dimension the source binding classifies into.</summary>
    private const long BranchDimension = 7000;

    /// <summary>The region dimension the bridge leads to.</summary>
    private const long RegionDimension = 8000;

    /// <summary>Bridge 40 leads from branch to region; bridge 50 from another dimension to region.</summary>
    private readonly InMemoryTableGateway<BridgeRecord> _bridges = new(Bridge(40, "branchToRegion", BranchDimension), Bridge(50, "productToRegion", 9000));

    /// <summary>The bound roles, whose keys name the bindings: branch (81), region (82) and regionAgain (83).</summary>
    private readonly InMemoryTableGateway<DimensionRoleRecord> _roles = new(
        DimensionRows.Role(81, "branch", BranchDimension),
        DimensionRows.Role(82, "region", RegionDimension),
        DimensionRows.Role(83, "regionAgain", RegionDimension));

    /// <summary>A bridge from the source binding's dimension to the bridge binding's dimension passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RouteFromTheSourceDimensionToTheBindingDimension_Passes()
    {
        Assert.Empty(await EvaluateAsync(Source(), BridgeBinding(sourceDimensionBindingId: 21, bridgeId: 40)));
    }

    /// <summary>A source binding that is not a binding of the version is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SourceBindingOfAnotherVersion_IsReported()
    {
        var otherVersionSource = Source() with { DatasetVersionId = 200 };

        var violation = Assert.Single(await EvaluateAsync(otherVersionSource, BridgeBinding(sourceDimensionBindingId: 21, bridgeId: 40)));

        Assert.Equal(BridgeBindingRouteRule.Code, violation.Code);
        Assert.Equal("Bridge binding 'region' remaps binding 21, which is not a binding of this version.", violation.Message);
    }

    /// <summary>A bridge that starts at another dimension than the source binding's is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BridgeFromAnotherDimension_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(Source(), BridgeBinding(sourceDimensionBindingId: 21, bridgeId: 50)));

        Assert.Equal("Bridge binding 'region' uses bridge 'productToRegion', which starts at another dimension than its source binding 'branch'.", violation.Message);
    }

    /// <summary>A bridge that ends at another dimension than the bridge binding's is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BridgeToAnotherDimension_IsReported()
    {
        var binding = BridgeBinding(sourceDimensionBindingId: 21, bridgeId: 40) with { DimensionId = 9999 };

        var violation = Assert.Single(await EvaluateAsync(Source(), binding));

        Assert.Equal("Bridge binding 'region' uses bridge 'branchToRegion', which ends at another dimension than the binding.", violation.Message);
    }

    /// <summary>Bridge bindings that remap each other form a route without a start; each one on it is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BridgeBindingsRemappingEachOther_AreReported()
    {
        var first = BridgeBinding(sourceDimensionBindingId: 23, bridgeId: 40);
        var second = BridgeBinding(sourceDimensionBindingId: 22, bridgeId: 40) with { DimensionBindingId = 23, DimensionRoleId = 83 };

        var cycles = (await EvaluateAsync(first, second)).Where(violation => violation.Message.EndsWith("leads back to itself through its source bindings.", StringComparison.Ordinal));

        Assert.Equal(["region", "regionAgain"], cycles.Select(violation => violation.Message.Split('\'')[1]));
    }

    /// <summary>Evaluates the rule on the given bindings.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params DimensionBindingRecord[] bindings)
    {
        var rule = new BridgeBindingRouteRule(
            new InMemoryTableGateway<DimensionBindingRecord>(bindings), _bridges, new BindingNames(_roles, new InMemoryTableGateway<FieldRecord>()));
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }

    /// <summary>The source binding 21: a mapped binding of the branch role.</summary>
    private static DimensionBindingRecord Source()
    {
        return DatasetRows.Binding(21, VersionId, 81, BindingKinds.Mapped, 1) with { DimensionId = BranchDimension, MappingSetId = 30 };
    }

    /// <summary>The bridge binding 22, of the region role.</summary>
    private static DimensionBindingRecord BridgeBinding(long sourceDimensionBindingId, long bridgeId)
    {
        return DatasetRows.Binding(22, VersionId, 82, BindingKinds.Bridge, null) with
        {
            DimensionId = RegionDimension,
            SourceDimensionBindingId = sourceDimensionBindingId,
            BridgeId = bridgeId
        };
    }

    /// <summary>A bridge to the region dimension.</summary>
    private static BridgeRecord Bridge(long bridgeId, string bridgeKey, long sourceDimensionId)
    {
        return new BridgeRecord
        {
            BridgeId = bridgeId,
            BridgeKey = bridgeKey,
            DisplayName = bridgeKey,
            SourceDimensionId = sourceDimensionId,
            TargetDimensionId = RegionDimension,
            IsEnabled = true
        };
    }
}
