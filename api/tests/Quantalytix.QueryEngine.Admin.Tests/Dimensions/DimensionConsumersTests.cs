using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms what keeps the keys that live objects name in a dimension: the published version of the dimension is what they
/// resolve their keys in, so <c>dimension_consumers_resolve</c> reports each key a new version would drop, and a member of the
/// published version that a live object names cannot be deleted. Live objects are the unknown members of bindings of published
/// dataset versions, the entries of published mapping versions, the mappings of published bridge versions, and the groupings of
/// enabled reports' calculation dimensions on the dimension's roles; drafts resolve nothing, so they never block. A dimension
/// that is a report's dimension, or whose role a calculation dimension reads, cannot be deleted at all.
/// </summary>
public sealed class DimensionConsumersTests
{
    /// <summary>The branch dimension.</summary>
    private const long BranchId = 400;

    /// <summary>The branch dimension's branch role.</summary>
    private const long BranchRoleId = 91;

    /// <summary>The published version of the branch dimension.</summary>
    private const long PublishedVersionId = 401;

    /// <summary>The draft version about to be published.</summary>
    private const long DraftVersionId = 402;

    /// <summary>The published v1 and the draft v2 of the branch dimension.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(
        DimensionRows.Version(PublishedVersionId, BranchId, 1, PublicationStatuses.Published),
        DimensionRows.Version(DraftVersionId, BranchId, 2, PublicationStatuses.Draft));

    /// <summary>The region level in both versions.</summary>
    private readonly InMemoryTableGateway<DimensionLevelRecord> _levels = new(
        DimensionRows.Level(1, PublishedVersionId, "region", 1),
        DimensionRows.Level(2, DraftVersionId, "region", 1));

    /// <summary>v1 holds every named member; the draft v2 drops BR02 and BR04.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        DimensionRows.Member(11, PublishedVersionId, "UNKNOWN"),
        DimensionRows.Member(12, PublishedVersionId, "BR01"),
        DimensionRows.Member(13, PublishedVersionId, "BR02"),
        DimensionRows.Member(14, PublishedVersionId, "BR03"),
        DimensionRows.Member(15, PublishedVersionId, "BR04"),
        DimensionRows.Member(21, DraftVersionId, "UNKNOWN"),
        DimensionRows.Member(22, DraftVersionId, "BR01"),
        DimensionRows.Member(24, DraftVersionId, "BR03"));

    /// <summary>
    /// A draft that drops BR02, which a published bridge maps, and BR04, which an enabled report's calculation dimension groups
    /// under, is reported for each; the keys the binding and the mapping entry name are kept, and BR09, which only a draft mapping
    /// version names, does not count.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task VersionDroppingKeysLiveObjectsName_IsReported()
    {
        var rule = new DimensionConsumersResolveRule(_versions, Consumers(), new PublishedDimensionReader(_versions, _levels, _members, new PublicationSet()));

        var violations = await rule.EvaluateAsync(DraftVersionId, TestContext.Current.CancellationToken);

        Assert.All(violations, violation => Assert.Equal(DimensionConsumersResolveRule.Code, violation.Rule));
        Assert.Equal(
            ["Bridge 'zz_map_bridge_70' names member 'BR02', which v2 lacks.",
             "Calculation dimension 'branch' of report 'balance_sheet' names member 'BR04', which v2 lacks."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>A member of the published version that a live mapping entry names cannot be deleted; the same key in the draft can.</summary>
    /// <returns>A task that completes when the delete checks are checked.</returns>
    [Fact]
    public async Task MemberALiveObjectNames_IsBlockedOnlyInThePublishedVersion()
    {
        var checks = Checks();
        var cancellationToken = TestContext.Current.CancellationToken;

        var published = await checks.MemberAsync(_members.All.Single(member => member.MemberId == 12), cancellationToken);
        var draft = await checks.MemberAsync(_members.All.Single(member => member.MemberId == 22), cancellationToken);

        Assert.Equal("Member 'BR01' cannot be deleted: named by Source key '06' of mapping set 'zz_map_codes_60'.", published);
        Assert.Null(draft);
    }

    /// <summary>
    /// The branch dimension's role is read by a calculation dimension, and the balance sheet's dimension holds its rows, so neither
    /// can be deleted.
    /// </summary>
    /// <returns>A task that completes when the delete checks are checked.</returns>
    [Fact]
    public async Task DimensionAReportOrCalculationDimensionNames_CannotBeDeleted()
    {
        var checks = Checks();
        var cancellationToken = TestContext.Current.CancellationToken;

        var branch = await checks.DimensionAsync(ReleaseRows.Dimension(BranchId, "branch"), [], cancellationToken);
        var rows = await checks.DimensionAsync(ReleaseRows.Dimension(ReportRows.ReportDimensionId, "balance_sheet"), [], cancellationToken);

        Assert.Equal("Dimension 'branch' cannot be deleted: read by calculation dimensions 'branch' of report 'balance_sheet'.", branch);
        Assert.Equal("Dimension 'balance_sheet' cannot be deleted: the report dimension of reports 'balance_sheet'.", rows);
    }

    /// <summary>The delete checks over the branch dimension's versions and the live objects.</summary>
    private DimensionDeleteChecks Checks()
    {
        return new DimensionDeleteChecks(
            _versions,
            _members,
            new InMemoryTableGateway<RangeBandRecord>(),
            Roles(),
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<BridgeRecord>(),
            new InMemoryTableGateway<DimensionBindingRecord>(),
            new InMemoryTableGateway<DatasetRecord>(),
            new InMemoryTableGateway<DatasetVersionRecord>(),
            Reports(),
            CalculationDimensions(),
            new InMemoryTableGateway<ReportSourceRecord>(),
            new ReportNames(Reports()),
            Consumers(),
            new DimensionChanges(_versions, _levels, _members, Consumers()));
    }

    /// <summary>The branch dimension's branch role.</summary>
    private static InMemoryTableGateway<DimensionRoleRecord> Roles()
    {
        return new InMemoryTableGateway<DimensionRoleRecord>(DimensionRows.Role(BranchRoleId, "branch", BranchId));
    }

    /// <summary>The enabled balance sheet.</summary>
    private static InMemoryTableGateway<ReportRecord> Reports()
    {
        return new InMemoryTableGateway<ReportRecord>(ReportRows.Report());
    }

    /// <summary>The balance sheet's calculation dimension on the branch role, which groups branches under BR04.</summary>
    private static InMemoryTableGateway<CalculationDimensionRecord> CalculationDimensions()
    {
        return new InMemoryTableGateway<CalculationDimensionRecord>(ReportRows.CalculationDimension(81, "branch", BranchRoleId) with
        {
            GroupingMode = GroupingModes.Ancestor,
            GroupingAncestorMemberKey = "BR04"
        });
    }

    /// <summary>
    /// The live objects: a binding of the branch role in a published dataset version that routes unmapped rows to UNKNOWN, a
    /// published mapping version mapping 06 to BR01 next to a draft mapping 99 to BR09, a published bridge from BR02, and the
    /// enabled balance sheet, whose calculation dimension groups branches under BR04.
    /// </summary>
    private static DimensionConsumers Consumers()
    {
        var roles = Roles();
        return new DimensionConsumers(
            new InMemoryTableGateway<DatasetRecord>(ReleaseRows.Dataset(1, "loans")),
            new InMemoryTableGateway<DatasetVersionRecord>(ReleaseRows.DatasetVersion(11, 1, 1, PublicationStatuses.Published)),
            new InMemoryTableGateway<DimensionBindingRecord>(ReleaseRows.Binding(31, 11, BranchRoleId, BranchId, BindingKinds.Direct) with
            {
                UnmappedBehavior = UnmappedBehaviors.Unknown,
                UnknownMemberKey = "UNKNOWN"
            }),
            roles,
            new InMemoryTableGateway<MappingSetRecord>(MappingRows.MappingSet(60, BranchId)),
            new InMemoryTableGateway<MappingVersionRecord>(
                MappingRows.MappingVersion(61, 60, 1, PublicationStatuses.Published),
                MappingRows.MappingVersion(62, 60, 2, PublicationStatuses.Draft)),
            new InMemoryTableGateway<DimensionMappingRecord>(MappingRows.Entry(1, 61, "06", "BR01"), MappingRows.Entry(2, 62, "99", "BR09")),
            new InMemoryTableGateway<BridgeRecord>(BridgeRows.Bridge(70, BranchId, 600)),
            new InMemoryTableGateway<BridgeVersionRecord>(BridgeRows.BridgeVersion(71, 70, 1, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Published)),
            new InMemoryTableGateway<BridgeMappingRecord>(BridgeRows.Mapping(71, "BR02", "EAST", 1m)),
            Reports(),
            CalculationDimensions(),
            new BindingNames(roles, new InMemoryTableGateway<FieldRecord>()));
    }
}
