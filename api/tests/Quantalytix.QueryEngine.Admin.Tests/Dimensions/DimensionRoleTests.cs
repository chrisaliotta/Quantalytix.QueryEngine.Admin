using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms the role lifecycle: a new dimension other than a time dimension comes with its first role, so bindings can bind it at
/// once, and a time dimension, whose bindings name the dimension, has none; a role key is unique in the tenant, because requests
/// name a binding by it; a report dimension gets no role, because no dataset binds a report's rows; a dimension whose role is a
/// scenario of a report gets no other role, because a scenario's dimension has exactly one role; a role that a binding, a
/// calculation dimension, a report source or a report scenario names is not deleted, naming each; and renaming a role advances
/// the semantic revision, because the query engine's catalog shows the name.
/// </summary>
public sealed class DimensionRoleTests
{
    /// <summary>The branch dimension, whose branch role exists.</summary>
    private const long BranchId = 500;

    /// <summary>The branch dimension's branch role.</summary>
    private const long BranchRoleId = 91;

    /// <summary>Report 62, the branch plan, which names the branch role as a scenario.</summary>
    private const long BranchPlanId = 62;

    /// <summary>The dimensions: branch.</summary>
    private readonly InMemoryTableGateway<DimensionRecord> _dimensions = new(ReleaseRows.Dimension(BranchId, "branch"));

    /// <summary>The roles: branch, of the branch dimension.</summary>
    private readonly InMemoryTableGateway<DimensionRoleRecord> _roles = new(DimensionRows.Role(BranchRoleId, "branch", BranchId));

    /// <summary>The bindings: the loans v1 binding of the branch role.</summary>
    private readonly InMemoryTableGateway<DimensionBindingRecord> _bindings = new(ReleaseRows.Binding(31, 11, BranchRoleId, BranchId, BindingKinds.Direct));

    /// <summary>The database info, at semantic revision 1.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "bank",
        MetadataSchemaVersion = 12,
        SemanticRevision = 1,
        UpdatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>A categorical dimension is created with its default role; a time dimension with none.</summary>
    /// <returns>A task that completes when the creates are checked.</returns>
    [Fact]
    public async Task NewDimension_ComesWithItsDefaultRoleUnlessItIsTime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = DimensionService();

        var region = await service.CreateAsync(NewDimension("region", DimensionKinds.Categorical) with { DefaultRole = NewRole("servicingRegion") }, cancellationToken);
        await service.CreateAsync(NewDimension("fiscal", DimensionKinds.Time), cancellationToken);

        var role = Assert.Single(_roles.All, row => row.DimensionRoleId != BranchRoleId);
        Assert.Equal(("servicingRegion", region.DimensionId), (role.DimensionRoleKey, role.DimensionId));
    }

    /// <summary>
    /// The default role is required for every kind but time and forbidden for time, and its key must be free among every
    /// dimension's roles.
    /// </summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task DefaultRole_IsRequiredUnlessTimeAndItsKeyMustBeFree()
    {
        var validator = new NewDimensionInputValidator(_dimensions, _roles);
        var cancellationToken = TestContext.Current.CancellationToken;

        var missing = await validator.TestValidateAsync(NewDimension("region", DimensionKinds.Categorical), cancellationToken: cancellationToken);
        var onTime = await validator.TestValidateAsync(NewDimension("fiscal", DimensionKinds.Time) with { DefaultRole = NewRole("fiscalPeriod") }, cancellationToken: cancellationToken);
        var taken = await validator.TestValidateAsync(NewDimension("region", DimensionKinds.Categorical) with { DefaultRole = NewRole("branch") }, cancellationToken: cancellationToken);

        missing.ShouldHaveValidationErrorFor(input => input.DefaultRole);
        onTime.ShouldHaveValidationErrorFor(input => input.DefaultRole);
        Assert.Equal("A role with key 'branch' already exists.", Assert.Single(taken.Errors).ErrorMessage);
    }

    /// <summary>A role of the branch dimension passes, while a role of a report dimension is refused.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task RoleOfAReportDimension_IsRefused()
    {
        const long StatementId = 700;
        var dimensions = new InMemoryTableGateway<DimensionRecord>(
            ReleaseRows.Dimension(BranchId, "branch"),
            ReleaseRows.Dimension(StatementId, "margin_statement") with { DimensionPurpose = DimensionPurposes.Report });
        var cancellationToken = TestContext.Current.CancellationToken;

        var onBranch = await RoleValidator(dimensions, new InMemoryTableGateway<ReportScenarioRecord>(), BranchId)
            .TestValidateAsync(NewRole("servicingBranch"), cancellationToken: cancellationToken);
        var onStatement = await RoleValidator(dimensions, new InMemoryTableGateway<ReportScenarioRecord>(), StatementId)
            .TestValidateAsync(NewRole("marginLine"), cancellationToken: cancellationToken);

        onBranch.ShouldNotHaveAnyValidationErrors();
        Assert.Equal("The dimension holds a report's rows, and a report dimension has no roles.", Assert.Single(onStatement.Errors).ErrorMessage);
    }

    /// <summary>
    /// Once the branch plan names the branch role as a scenario, the branch dimension takes no other role: a scenario's dimension has
    /// exactly one role, so the scenario is its dimension, and requests and datasets reach its members through that one role.
    /// </summary>
    /// <returns>A task that completes when the input is validated.</returns>
    [Fact]
    public async Task DimensionOfAScenario_TakesNoOtherRole()
    {
        var result = await RoleValidator(_dimensions, Scenarios(), BranchId)
            .TestValidateAsync(NewRole("servicingBranch"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            "Role 'branch' of the dimension is a scenario of report 'branch_plan', and a scenario's dimension has exactly one role.",
            Assert.Single(result.Errors).ErrorMessage);
    }

    /// <summary>A role a binding, a calculation dimension, a bridge source and a report scenario name is kept, and the detail names each.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task RoleInUse_IsNotDeleted()
    {
        var detail = await RoleService().DeleteAsync(BranchRoleId, TestContext.Current.CancellationToken);

        Assert.Equal(
            "Role 'branch' cannot be deleted: bound by dataset versions 'loans' v1; read by calculation dimensions 'branch' of report 'balance_sheet'; " +
            "bridged from by report sources 'gl_bridged' of report 'balance_sheet'; named as a scenario by report 'branch_plan'.",
            detail);
        Assert.Single(_roles.All);
    }

    /// <summary>Renaming a role advances the semantic revision; saving it unchanged does not.</summary>
    /// <returns>A task that completes when the updates are checked.</returns>
    [Fact]
    public async Task RenamingARole_AdvancesTheSemanticRevision()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = RoleService();

        await service.UpdateAsync(BranchRoleId, new DimensionRoleInput { DisplayName = "branch" }, cancellationToken);
        Assert.Equal(1, Assert.Single(_databaseInfo.All).SemanticRevision);

        var renamed = await service.UpdateAsync(BranchRoleId, new DimensionRoleInput { DisplayName = "Servicing branch", Description = "The branch that services the loan." }, cancellationToken);
        Assert.Equal(("branch", "Servicing branch"), (renamed.DimensionRoleKey, renamed.DisplayName));
        Assert.Equal(2, Assert.Single(_databaseInfo.All).SemanticRevision);
    }

    /// <summary>A new user dimension of a kind, with no default role.</summary>
    private static NewDimensionInput NewDimension(string dimensionKey, string kind)
    {
        return new NewDimensionInput { DimensionKey = dimensionKey, DisplayName = dimensionKey, DimensionKind = kind, IsEnabled = true, IsRemovable = true };
    }

    /// <summary>A new role whose key is also its display name.</summary>
    private static NewDimensionRoleInput NewRole(string dimensionRoleKey)
    {
        return new NewDimensionRoleInput { DimensionRoleKey = dimensionRoleKey, DisplayName = dimensionRoleKey };
    }

    /// <summary>The route of a request to a dimension's roles.</summary>
    private static FixedRequestRoute Route(long dimensionId)
    {
        return new FixedRequestRoute(new Dictionary<string, long> { ["dimensionId"] = dimensionId });
    }

    /// <summary>The validator of a new role of a dimension, over the test roles and the given dimensions and scenarios.</summary>
    private NewDimensionRoleInputValidator RoleValidator(
        InMemoryTableGateway<DimensionRecord> dimensions, InMemoryTableGateway<ReportScenarioRecord> scenarios, long dimensionId)
    {
        return new NewDimensionRoleInputValidator(_roles, dimensions, scenarios, new ReportNames(Reports()), Route(dimensionId));
    }

    /// <summary>The balance sheet and the branch plan.</summary>
    private static InMemoryTableGateway<ReportRecord> Reports()
    {
        return new InMemoryTableGateway<ReportRecord>(ReportRows.Report(), ReportRows.Report() with { ReportId = BranchPlanId, ReportKey = "branch_plan" });
    }

    /// <summary>The branch plan's scenario on the branch role.</summary>
    private static InMemoryTableGateway<ReportScenarioRecord> Scenarios()
    {
        return new InMemoryTableGateway<ReportScenarioRecord>(ReportRows.Scenario(BranchRoleId) with { ReportId = BranchPlanId });
    }

    /// <summary>The dimension service over the test tables.</summary>
    private DimensionService DimensionService()
    {
        var ids = new SequentialIdGenerator(1000);
        var versions = new InMemoryTableGateway<DimensionVersionRecord>();
        var levels = new InMemoryTableGateway<DimensionLevelRecord>();
        var members = new InMemoryTableGateway<DimensionMemberRecord>();
        var bands = new InMemoryTableGateway<RangeBandRecord>();
        var closure = new InMemoryTableGateway<DimensionClosureRecord>();
        var remover = new DimensionVersionRemover(versions, levels, members, closure, bands);
        return new DimensionService(
            _dimensions,
            versions,
            _roles,
            new DimensionVersionService(versions, new DimensionVersionCloner(levels, members, bands, ids), remover, ids),
            RoleService(),
            new DimensionMemberService(members, Checks(), new ClosureBuilder(members, closure), ids),
            remover,
            Checks(),
            new SemanticRevision(_databaseInfo, new FakeTimeProvider()),
            ids);
    }

    /// <summary>The role service over the test tables.</summary>
    private DimensionRoleService RoleService()
    {
        return new DimensionRoleService(_roles, Checks(), new SemanticRevision(_databaseInfo, new FakeTimeProvider()), new SequentialIdGenerator(2000));
    }

    /// <summary>
    /// The delete checks over the test tables: the balance sheet reads the branch role through calculation dimension
    /// <c>branch</c> and bridges from it in source <c>gl_bridged</c>, and the branch plan names it as a scenario.
    /// </summary>
    private DimensionDeleteChecks Checks()
    {
        var reports = Reports();
        var datasets = new InMemoryTableGateway<DatasetRecord>(ReleaseRows.Dataset(1, "loans"));
        var datasetVersions = new InMemoryTableGateway<DatasetVersionRecord>(ReleaseRows.DatasetVersion(11, 1, 1, PublicationStatuses.Published));
        var calculationDimensions = new InMemoryTableGateway<CalculationDimensionRecord>(ReportRows.CalculationDimension(81, "branch", BranchRoleId));
        var consumers = new DimensionConsumers(
            datasets, datasetVersions, _bindings, _roles,
            new InMemoryTableGateway<MappingSetRecord>(), new InMemoryTableGateway<MappingVersionRecord>(), new InMemoryTableGateway<DimensionMappingRecord>(),
            new InMemoryTableGateway<BridgeRecord>(), new InMemoryTableGateway<BridgeVersionRecord>(), new InMemoryTableGateway<BridgeMappingRecord>(),
            reports, calculationDimensions, new BindingNames(_roles, new InMemoryTableGateway<FieldRecord>()));
        return new DimensionDeleteChecks(
            new InMemoryTableGateway<DimensionVersionRecord>(),
            new InMemoryTableGateway<DimensionMemberRecord>(),
            new InMemoryTableGateway<RangeBandRecord>(),
            _roles,
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<BridgeRecord>(),
            _bindings,
            datasets,
            datasetVersions,
            reports,
            calculationDimensions,
            new InMemoryTableGateway<ReportSourceRecord>(ReportRows.Source(72, "gl_bridged", 1) with
            {
                MappingMethod = ReportMappingMethods.Bridge,
                FieldKey = null,
                DimensionRoleId = BranchRoleId,
                BridgeId = 95
            }),
            Scenarios(),
            new ReportNames(reports),
            consumers);
    }
}
