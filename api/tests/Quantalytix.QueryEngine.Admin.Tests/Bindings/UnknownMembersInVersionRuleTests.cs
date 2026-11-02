using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms that <c>unknown_members_in_version</c> resolves a binding's unknown member key in its dimension's version in the
/// dataset version's own snapshot, and ignores the dimension's versions in other snapshots. A binding with the key whose dimension
/// has no version in that snapshot is reported too. The engine resolves the key in that version, so a key it lacks routes
/// unmapped rows nowhere.
/// </summary>
public sealed class UnknownMembersInVersionRuleTests
{
    /// <summary>The dataset version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The bound dimension.</summary>
    private const long DimensionId = 7000;

    /// <summary>A version in the frozen snapshot.</summary>
    private const long V1 = 701;

    /// <summary>The live version.</summary>
    private const long V2 = 702;

    /// <summary>Another version in the frozen snapshot.</summary>
    private const long V3 = 703;

    /// <summary>The members: UNKNOWN in v2, LEGACY in v1 and PENDING in v3 only.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        DimensionRows.Member(7013, V1, "LEGACY"),
        DimensionRows.Member(7022, V2, "UNKNOWN"),
        DimensionRows.Member(7033, V3, "PENDING"));

    /// <summary>An unknown member the live version has passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeyInTheSnapshotsVersion_Passes()
    {
        Assert.Empty(await EvaluateAsync(Binding("UNKNOWN"), Versions()));
    }

    /// <summary>An unknown member that only versions in another snapshot have is reported against the live version.</summary>
    /// <param name="memberKey">The member the binding routes unmapped rows to.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData("LEGACY")]
    [InlineData("PENDING")]
    public async Task KeyOutsideTheSnapshotsVersion_IsReported(string memberKey)
    {
        var violation = Assert.Single(await EvaluateAsync(Binding(memberKey), Versions()));

        Assert.Equal("unknown_members_in_version", violation.Code);
        Assert.Equal(
            $"Binding 'region' routes unmapped rows to member '{memberKey}', which is not a member of its dimension's version.",
            violation.Message);
    }

    /// <summary>A binding with an unknown member whose dimension has no version in the snapshot is reported once.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeyWithoutAVersionInTheSnapshot_IsReported()
    {
        var frozenOnly = new InMemoryTableGateway<DimensionVersionRecord>(DimensionRows.Version(V3, DimensionId, SnapshotRows.FrozenId));

        var violation = Assert.Single(await EvaluateAsync(Binding("UNKNOWN"), frozenOnly));

        Assert.Equal("Binding 'region' routes unmapped rows to a member of its dimension, which has no version in this snapshot.", violation.Message);
    }

    /// <summary>The dimension's versions: v2 live, v1 and v3 frozen.</summary>
    private static InMemoryTableGateway<DimensionVersionRecord> Versions()
    {
        return new InMemoryTableGateway<DimensionVersionRecord>(
            DimensionRows.Version(V1, DimensionId, SnapshotRows.FrozenId),
            DimensionRows.Version(V2, DimensionId),
            DimensionRows.Version(V3, DimensionId, SnapshotRows.FrozenId));
    }

    /// <summary>Evaluates the rule on a version holding one binding, over the given dimension versions.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DimensionBindingRecord binding, InMemoryTableGateway<DimensionVersionRecord> versions)
    {
        var rule = new UnknownMembersInVersionRule(new SnapshotDimensionReader(versions, new InMemoryTableGateway<DimensionLevelRecord>(), _members));
        var snapshot = DatasetRows.Snapshot(
            fields: [DatasetRows.ColumnField(1, VersionId, "regionCode", "region_code")],
            bindings: [binding],
            roles: [DimensionRows.Role(81, "region", DimensionId)]);
        return rule.EvaluateAsync(snapshot, TestContext.Current.CancellationToken);
    }

    /// <summary>A mapped binding of the region role that routes unmapped rows to a member.</summary>
    private static DimensionBindingRecord Binding(string unknownMemberKey)
    {
        return DatasetRows.Binding(21, VersionId, 81, BindingKinds.Mapped, 1) with
        {
            MappingSetId = 30,
            UnmappedBehavior = UnmappedBehaviors.Unknown,
            UnknownMemberKey = unknownMemberKey
        };
    }
}
