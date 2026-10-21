using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms that the scenarios an enabled report's default comparisons read survive edits and publishes of the scenario dimension,
/// because every request that takes the defaults resolves them in its published version: a version a comparison names must stay an
/// assignable member, and a folder whose latest version a comparison reads must keep an assignable member under it. Publishing a
/// version that breaks either is reported, and an edit or delete of the published version that would break either is refused before
/// anything is written; keys no enabled report's comparison reads stay free to change.
/// </summary>
public sealed class ScenarioConsumersTests
{
    /// <summary>The scenario dimension.</summary>
    private const long ScenarioDimensionId = 620;

    /// <summary>The scenario dimension's role, which the report's scenario dimension reads.</summary>
    private const long ScenarioRoleId = 88;

    /// <summary>The published v1 of the scenario dimension.</summary>
    private const long PublishedVersionId = 621;

    /// <summary>The draft v2 of the scenario dimension.</summary>
    private const long DraftVersionId = 622;

    /// <summary>The published v1 and the draft v2.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(
        DimensionRows.Version(PublishedVersionId, ScenarioDimensionId, 1, PublicationStatuses.Published),
        DimensionRows.Version(DraftVersionId, ScenarioDimensionId, 2, PublicationStatuses.Draft));

    /// <summary>No levels.</summary>
    private readonly InMemoryTableGateway<DimensionLevelRecord> _levels = new();

    /// <summary>
    /// v1 files budget versions BUD_FY26_V1 and BUD_FY26_V2 under folder BUDGETS and forecast version FC_FY26_V1 under folder
    /// FORECASTS; the draft v2 makes BUD_FY26_V2 unassignable and leaves FORECASTS without a version.
    /// </summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        DimensionRows.Member(31, PublishedVersionId, "BUDGETS") with { IsAssignable = false },
        DimensionRows.Member(32, PublishedVersionId, "BUD_FY26_V1", parentId: 31),
        DimensionRows.Member(33, PublishedVersionId, "BUD_FY26_V2", parentId: 31),
        DimensionRows.Member(34, PublishedVersionId, "FORECASTS") with { IsAssignable = false },
        DimensionRows.Member(35, PublishedVersionId, "FC_FY26_V1", parentId: 34),
        DimensionRows.Member(41, DraftVersionId, "BUDGETS") with { IsAssignable = false },
        DimensionRows.Member(43, DraftVersionId, "BUD_FY26_V2", parentId: 41) with { IsAssignable = false },
        DimensionRows.Member(44, DraftVersionId, "FORECASTS") with { IsAssignable = false });

    /// <summary>The enabled balance sheet, whose default comparisons read budget BUD_FY26_V2 and the latest version under FORECASTS.</summary>
    private readonly InMemoryTableGateway<ReportRecord> _reports = new(ReportRows.Report() with
    {
        DefaultSettingsJson = """{"comparisons":[{"key":"bud","scenario":"BUD_FY26_V2","transforms":["change"]},{"key":"fc","scenario":{"latest":"FORECASTS"},"transforms":["change"]}]}"""
    });

    /// <summary>A version a default comparison reads must stay assignable, and a folder it reads the latest version of must keep one under it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PublishingAVersionThatLosesTheScenarios_IsReported()
    {
        var rule = new DimensionConsumersResolveRule(_versions, Consumers(), new PublishedDimensionReader(_versions, _levels, _members, new PublicationSet()));

        var violations = await rule.EvaluateAsync(DraftVersionId, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["Default comparison 'bud' of report 'balance_sheet' names member 'BUD_FY26_V2', which is not an assignable member of v2.",
             "Default comparison 'fc' of report 'balance_sheet' names member 'FORECASTS', under which v2 has no assignable member."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>
    /// In the published version, making BUD_FY26_V2 unassignable and moving FC_FY26_V1 out of FORECASTS are refused and leave the
    /// members as they were, while making BUD_FY26_V1 unassignable, which no comparison reads, is written.
    /// </summary>
    /// <returns>A task that completes when the edits are checked.</returns>
    [Fact]
    public async Task EditsThatStopAScenarioResolving_AreRefused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var members = Members();

        var unassigned = await members.UpdateAsync(Reference(33), Input(33) with { IsAssignable = false }, cancellationToken);
        var moved = await members.ReorderAsync(
            PublishedVersionId, new MemberOrderInput { Items = [new MemberPlacement { MemberId = 35, ParentMemberId = null, SortOrder = 5 }] }, cancellationToken);
        var free = await members.UpdateAsync(Reference(32), Input(32) with { IsAssignable = false }, cancellationToken);

        Assert.Equal(
            ["Default comparison 'bud' of report 'balance_sheet' names member 'BUD_FY26_V2', which is not an assignable member of v1."],
            unassigned.Select(violation => violation.Message));
        Assert.Equal(
            ["Default comparison 'fc' of report 'balance_sheet' names member 'FORECASTS', under which v1 has no assignable member."],
            moved.Select(violation => violation.Message));
        Assert.Empty(free);
        Assert.True(Member(33).IsAssignable);
        Assert.Equal(34, Member(35).ParentMemberId);
        Assert.False(Member(32).IsAssignable);
    }

    /// <summary>
    /// Deleting BUD_FY26_V2, which a comparison names, or FC_FY26_V1, the last version under FORECASTS, is blocked with what needs
    /// it; BUD_FY26_V1, which nothing reads, is deleted.
    /// </summary>
    /// <returns>A task that completes when the deletes are checked.</returns>
    [Fact]
    public async Task DeletesThatStopAScenarioResolving_AreBlocked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var members = Members();

        var named = await members.DeleteAsync(Reference(33), cancellationToken);
        var last = await members.DeleteAsync(Reference(35), cancellationToken);
        var free = await members.DeleteAsync(Reference(32), cancellationToken);

        Assert.Equal("Member 'BUD_FY26_V2' cannot be deleted: named by Default comparison 'bud' of report 'balance_sheet'.", named);
        Assert.Equal(
            "Member 'FC_FY26_V1' cannot be deleted: needed by Default comparison 'fc' of report 'balance_sheet' (an assignable member under 'FORECASTS').",
            last);
        Assert.Null(free);
        Assert.DoesNotContain(_members.All, member => member.MemberId == 32);
    }

    /// <summary>A disabled report's comparisons hold nothing, because the engine does not read the report; enabling it checks them again.</summary>
    /// <returns>A task that completes when the edit is checked.</returns>
    [Fact]
    public async Task ScenariosOfADisabledReport_HoldNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _reports.UpdateAsync(Assert.Single(_reports.All) with { IsEnabled = false }, cancellationToken);

        var violations = await Members().UpdateAsync(Reference(33), Input(33) with { IsAssignable = false }, cancellationToken);

        Assert.Empty(violations);
        Assert.False(Member(33).IsAssignable);
    }

    /// <summary>A member of the published version.</summary>
    private static MemberReference Reference(long memberId)
    {
        return new MemberReference { DimensionVersionId = PublishedVersionId, MemberId = memberId };
    }

    /// <summary>A stored member as an edit that changes nothing would send it.</summary>
    private MemberInput Input(long memberId)
    {
        var member = Member(memberId);
        return new MemberInput
        {
            DisplayName = member.DisplayName,
            ParentMemberId = member.ParentMemberId,
            LevelId = member.LevelId,
            IsAssignable = member.IsAssignable,
            SortOrder = member.SortOrder
        };
    }

    /// <summary>A stored member.</summary>
    private DimensionMemberRecord Member(long memberId)
    {
        return _members.All.Single(member => member.MemberId == memberId);
    }

    /// <summary>The member service over these tables.</summary>
    private DimensionMemberService Members()
    {
        var consumers = Consumers();
        var changes = new DimensionChanges(_versions, _levels, _members, consumers);
        var checks = new DimensionDeleteChecks(
            _versions,
            _members,
            new InMemoryTableGateway<RangeBandRecord>(),
            Roles(),
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<BridgeRecord>(),
            new InMemoryTableGateway<DimensionBindingRecord>(),
            new InMemoryTableGateway<DatasetRecord>(),
            new InMemoryTableGateway<DatasetVersionRecord>(),
            _reports,
            CalculationDimensions(),
            new InMemoryTableGateway<ReportSourceRecord>(),
            new ReportNames(_reports),
            consumers,
            changes);
        return new DimensionMemberService(
            _members, checks, changes, new ClosureBuilder(_members, new InMemoryTableGateway<DimensionClosureRecord>()), new SequentialIdGenerator(5000));
    }

    /// <summary>The live objects: the balance sheet's scenario dimension and its default comparisons, and nothing else.</summary>
    private DimensionConsumers Consumers()
    {
        var roles = Roles();
        return new DimensionConsumers(
            new InMemoryTableGateway<DatasetRecord>(),
            new InMemoryTableGateway<DatasetVersionRecord>(),
            new InMemoryTableGateway<DimensionBindingRecord>(),
            roles,
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<MappingVersionRecord>(),
            new InMemoryTableGateway<DimensionMappingRecord>(),
            new InMemoryTableGateway<BridgeRecord>(),
            new InMemoryTableGateway<BridgeVersionRecord>(),
            new InMemoryTableGateway<BridgeMappingRecord>(),
            _reports,
            CalculationDimensions(),
            new BindingNames(roles, new InMemoryTableGateway<FieldRecord>()));
    }

    /// <summary>The scenario dimension's role.</summary>
    private static InMemoryTableGateway<DimensionRoleRecord> Roles()
    {
        return new InMemoryTableGateway<DimensionRoleRecord>(DimensionRows.Role(ScenarioRoleId, "scenario", ScenarioDimensionId));
    }

    /// <summary>The balance sheet's scenario dimension on the scenario role.</summary>
    private static InMemoryTableGateway<CalculationDimensionRecord> CalculationDimensions()
    {
        return new InMemoryTableGateway<CalculationDimensionRecord>(
            ReportRows.CalculationDimension(82, "scenario", ScenarioRoleId) with { IsGroupable = false, IsFilterable = false, IsScenario = true });
    }
}
