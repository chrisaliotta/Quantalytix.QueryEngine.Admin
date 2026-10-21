using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.ReportDesigns;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportDesigns;

/// <summary>
/// The tables of one report design and the design services over them. Report 60's version 61 and its report dimension 500's
/// version 501 hold the rows TOTAL, LOANS and CASH; source <c>gl_balance</c> classifies through the role of account dimension 800,
/// whose published version holds accounts 1001, 1002 and 1003, and bridge 90, whose version 91 maps 1001 to LOANS and 1002 to
/// CASH. Versions 61, 501 and 91 share the status the fixture is built with.
/// </summary>
internal sealed class ReportDesignFixture
{
    /// <summary>The report dimension's version that holds the rows.</summary>
    public const long StructureVersionId = 501;

    /// <summary>The analytical dimension of accounts, which the source binds.</summary>
    public const long AccountDimensionId = 800;

    /// <summary>The published version of the account dimension.</summary>
    public const long AccountVersionId = 801;

    /// <summary>The account role the source's dataset binds.</summary>
    public const long AccountRoleId = 85;

    /// <summary>The source's bridge from accounts into the report dimension.</summary>
    public const long BridgeId = 90;

    /// <summary>The bridge's version that holds the row mappings.</summary>
    public const long BridgeVersionId = 91;

    /// <summary>Allocates the IDs of new rows.</summary>
    private readonly SequentialIdGenerator _ids = new(5000);

    /// <summary>Builds the design with every version of it in one status.</summary>
    /// <param name="status">The status of the report version, the report dimension's version and the bridge's version.</param>
    public ReportDesignFixture(string status)
    {
        ReportVersions = new(ReportRows.ReportVersion(ReportRows.ReportVersionId, status));
        DimensionVersions = new(
            DimensionRows.Version(StructureVersionId, ReportRows.ReportDimensionId, 1, status),
            DimensionRows.Version(AccountVersionId, AccountDimensionId, 1, PublicationStatuses.Published));
        BridgeVersions = new(BridgeRows.BridgeVersion(BridgeVersionId, BridgeId, 1, Cardinalities.ManyToOne, AllocationModes.None, status));
    }

    /// <summary>Report 60, whose report dimension is 500.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(ReportRows.Report());

    /// <summary>The report's versions.</summary>
    public InMemoryTableGateway<ReportVersionRecord> ReportVersions { get; }

    /// <summary>The report dimension and the account dimension.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(
        ReleaseRows.Dimension(ReportRows.ReportDimensionId, "balance_sheet") with { DimensionPurpose = DimensionPurposes.Report },
        ReleaseRows.Dimension(AccountDimensionId, "account"));

    /// <summary>The versions of both dimensions.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; }

    /// <summary>The rows TOTAL, a group, and LOANS and CASH, data rows under it; and the accounts.</summary>
    public InMemoryTableGateway<DimensionMemberRecord> Members { get; } = new(
        DimensionRows.Member(1, StructureVersionId, "TOTAL") with { IsAssignable = false },
        DimensionRows.Member(2, StructureVersionId, "LOANS", parentId: 1),
        DimensionRows.Member(3, StructureVersionId, "CASH", parentId: 1) with { SortOrder = 1 },
        DimensionRows.Member(11, AccountVersionId, "1001"),
        DimensionRows.Member(12, AccountVersionId, "1002"),
        DimensionRows.Member(13, AccountVersionId, "1003"));

    /// <summary>The account role.</summary>
    public InMemoryTableGateway<DimensionRoleRecord> Roles { get; } = new(DimensionRows.Role(AccountRoleId, "account", AccountDimensionId));

    /// <summary>The source's bridge.</summary>
    public InMemoryTableGateway<BridgeRecord> Bridges { get; } = new(BridgeRows.Bridge(BridgeId, AccountDimensionId, ReportRows.ReportDimensionId));

    /// <summary>The bridge's versions.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; }

    /// <summary>The row mappings: 1001 to LOANS and 1002 to CASH.</summary>
    public InMemoryTableGateway<BridgeMappingRecord> BridgeMappings { get; } = new(
        BridgeRows.Mapping(BridgeVersionId, "1001", "LOANS", 1m),
        BridgeRows.Mapping(BridgeVersionId, "1002", "CASH", 1m));

    /// <summary>The source's one variable source, which classifies through the bridge.</summary>
    public InMemoryTableGateway<VariableSourceRecord> Sources { get; } = new(ReportRows.Source(70, "balance", "gl_balance", 1) with
    {
        ClassificationKind = ClassificationKinds.Bridge,
        ClassificationFieldKey = null,
        ClassificationRoleId = AccountRoleId,
        BridgeId = BridgeId
    });

    /// <summary>The member options; none at first.</summary>
    public InMemoryTableGateway<ReportMemberOptionRecord> MemberOptions { get; } = new();

    /// <summary>The overlay rows; none at first.</summary>
    public InMemoryTableGateway<ReportLineRecord> Lines { get; } = new();

    /// <summary>The variable scopes; none at first.</summary>
    public InMemoryTableGateway<ReportVariableScopeRecord> Scopes { get; } = new();

    /// <summary>The mapping sets; none map into the report dimension at first.</summary>
    public InMemoryTableGateway<MappingSetRecord> MappingSets { get; } = new();

    /// <summary>The mapping set versions.</summary>
    public InMemoryTableGateway<MappingVersionRecord> MappingVersions { get; } = new();

    /// <summary>The mapping entries.</summary>
    public InMemoryTableGateway<DimensionMappingRecord> MappingEntries { get; } = new();

    /// <summary>The report columns; none.</summary>
    private InMemoryTableGateway<ReportColumnRecord> Columns { get; } = new();

    /// <summary>The closure rows the member writes rebuild.</summary>
    private InMemoryTableGateway<DimensionClosureRecord> Closure { get; } = new();

    /// <summary>The design's pairing.</summary>
    /// <returns>The pairing over these tables.</returns>
    public ReportDesignVersions Designs()
    {
        return new ReportDesignVersions(Reports, ReportVersions, DimensionVersions, Bridges, BridgeVersions);
    }

    /// <summary>The design reader.</summary>
    /// <returns>The reader over these tables.</returns>
    public ReportDesignReader Reader()
    {
        return new ReportDesignReader(Designs(), Members, MemberOptions, Lines, Columns, Sources, BridgeMappings, MappingSets, MappingVersions, MappingEntries);
    }

    /// <summary>The row tree service.</summary>
    /// <returns>The service over these tables.</returns>
    public DesignRowService Rows()
    {
        var members = new DimensionMemberService(Members, Checks(), new ClosureBuilder(Members, Closure), _ids);
        return new DesignRowService(
            Designs(),
            Reader(),
            members,
            new ReportMemberOptionService(MemberOptions),
            new ReportLineService(Lines, Columns, _ids),
            new ReportVariableScopeService(Scopes),
            new BridgeMappingService(BridgeMappings, _ids),
            Lines);
    }

    /// <summary>The row tree validator, for a request to report version 61.</summary>
    /// <returns>The validator over these tables.</returns>
    public DesignRowsInputValidator RowsValidator()
    {
        var metrics = new ModelMetricsReader(
            ReportVersions,
            new InMemoryTableGateway<CalculationModelVersionRecord>(),
            new InMemoryTableGateway<VariableRecord>(),
            new InMemoryTableGateway<CalculatedMeasureRecord>());
        var keys = new VersionKeys(new FixedRequestRoute(new Dictionary<string, long> { ["versionId"] = ReportRows.ReportVersionId }));
        return new DesignRowsInputValidator(Designs(), Members, Lines, Sources, BridgeMappings, MappingSets, MappingVersions, MappingEntries, metrics, Columns, keys);
    }

    /// <summary>The row mapping service.</summary>
    /// <returns>The service over these tables.</returns>
    public RowMappingService RowMappings()
    {
        return new RowMappingService(Designs(), Reader(), new BridgeMappingService(BridgeMappings, _ids), Sources);
    }

    /// <summary>The paired draft service.</summary>
    /// <returns>The service over these tables.</returns>
    public ReportDesignDrafts Drafts()
    {
        var levels = new InMemoryTableGateway<DimensionLevelRecord>();
        var bands = new InMemoryTableGateway<RangeBandRecord>();
        var profiles = new InMemoryTableGateway<SourceProfileRecord>();
        var picks = new InMemoryTableGateway<SourceProfileVariableRecord>();
        var calculationDimensions = new InMemoryTableGateway<CalculationDimensionRecord>();
        var reportVersions = new ReportVersionService(
            ReportVersions,
            new ReportVersionCloner(Scopes, Sources, profiles, picks, calculationDimensions, Columns, Lines, MemberOptions, _ids),
            new ReportContentRemover(Sources, profiles, picks, calculationDimensions, Scopes, Columns, Lines, MemberOptions),
            _ids);
        var dimensionVersions = new DimensionVersionService(
            DimensionVersions,
            new DimensionVersionCloner(levels, Members, bands, _ids),
            new DimensionVersionRemover(DimensionVersions, levels, Members, Closure, bands),
            _ids);
        var bridgeVersions = new BridgeVersionService(BridgeVersions, BridgeMappings, new BridgeVersionCloner(BridgeMappings, _ids), _ids);
        var databaseInfo = new InMemoryTableGateway<DatabaseInfoRecord>(new DatabaseInfoRecord
        {
            SingletonId = 1,
            TenantKey = "bank",
            MetadataSchemaVersion = 9,
            SemanticRevision = 1,
            UpdatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        var bridges = new BridgeService(
            Bridges,
            BridgeVersions,
            Dimensions,
            new InMemoryTableGateway<DimensionBindingRecord>(),
            Sources,
            new BindingNames(Roles, new InMemoryTableGateway<FieldRecord>()),
            new ReportVersionNames(Reports, ReportVersions),
            bridgeVersions,
            new SemanticRevision(databaseInfo, new FakeTimeProvider()),
            _ids);
        return new ReportDesignDrafts(Reports, DimensionVersions, Bridges, BridgeVersions, reportVersions, dimensionVersions, bridgeVersions, bridges);
    }

    /// <summary>The member delete checks over these tables, whose live consumers are read through an empty publication set.</summary>
    private DimensionDeleteChecks Checks()
    {
        var datasets = new InMemoryTableGateway<DatasetRecord>();
        var datasetVersions = new InMemoryTableGateway<DatasetVersionRecord>();
        var bindings = new InMemoryTableGateway<DimensionBindingRecord>();
        var calculationDimensions = new InMemoryTableGateway<CalculationDimensionRecord>();
        var consumers = new DimensionConsumers(
            datasets, datasetVersions, bindings, Roles, MappingSets, MappingVersions, MappingEntries, Bridges, BridgeVersions, BridgeMappings,
            Reports, ReportVersions, MemberOptions, Lines, Scopes, calculationDimensions,
            new BindingNames(Roles, new InMemoryTableGateway<FieldRecord>()), new PublicationSet());
        return new DimensionDeleteChecks(
            DimensionVersions, Members, new InMemoryTableGateway<RangeBandRecord>(), Roles, MappingSets, Bridges, bindings, datasets, datasetVersions,
            Reports, calculationDimensions, Sources, new ReportVersionNames(Reports, ReportVersions), consumers);
    }
}
