using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Snapshots;

/// <summary>
/// The report fixture's tenant, whose live snapshot holds dataset GL, the account and report dimensions, bridge 90, model 40 and
/// report 60, plus mapping set 300 onto the account dimension with live version 301 mapping <c>06</c> to account 1001: seven live
/// versions of the six kinds. The snapshot commands run over the kinds' real copiers and removers. The kinds' rules are their own tests' concern, so each test passes the version rules restore runs.
/// </summary>
internal sealed class SnapshotFixture
{
    /// <summary>The mapping set onto accounts.</summary>
    public const long MappingSetId = 300;

    /// <summary>Its live version.</summary>
    public const long MappingVersionId = 301;

    /// <summary>The id generator every service shares, so their ids never collide.</summary>
    private readonly SequentialIdGenerator _ids = new(20_000);

    /// <summary>The clock.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));

    /// <summary>The report fixture's tables.</summary>
    public ReportFixture Tables { get; } = new();

    /// <summary>The mapping set table.</summary>
    public InMemoryTableGateway<MappingSetRecord> MappingSets { get; } = new(MappingRows.MappingSet(MappingSetId, ReportFixture.AccountDimensionId));

    /// <summary>The mapping version table.</summary>
    public InMemoryTableGateway<MappingVersionRecord> MappingVersions { get; } = new(MappingRows.MappingVersion(MappingVersionId, MappingSetId));

    /// <summary>The mapping entry table.</summary>
    public InMemoryTableGateway<DimensionMappingRecord> Entries { get; } = new(MappingRows.Entry(302, MappingVersionId, "06", "1001"));

    /// <summary>The restore log.</summary>
    public InMemoryTableGateway<SnapshotRestoreRecord> Restores { get; } = new();

    /// <summary>The dataset grains; none.</summary>
    private InMemoryTableGateway<DatasetGrainRecord> Grains { get; } = new();

    /// <summary>The grain fields; none.</summary>
    private InMemoryTableGateway<DatasetGrainFieldRecord> GrainFields { get; } = new();

    /// <summary>The measure components; none.</summary>
    private InMemoryTableGateway<MeasureComponentRecord> Components { get; } = new();

    /// <summary>The measure dependencies; none.</summary>
    private InMemoryTableGateway<MeasureDependencyRecord> Dependencies { get; } = new();

    /// <summary>The measure time policies; none.</summary>
    private InMemoryTableGateway<MeasureTimePolicyRecord> TimePolicies { get; } = new();

    /// <summary>The range bands; none.</summary>
    private InMemoryTableGateway<RangeBandRecord> Bands { get; } = new();

    /// <summary>The metric attributions; none.</summary>
    private InMemoryTableGateway<MetricAttributionRecord> Attributions { get; } = new();

    /// <summary>The attribution drivers; none.</summary>
    private InMemoryTableGateway<MetricAttributionDriverRecord> AttributionDrivers { get; } = new();

    /// <summary>The tenant's semantic revision.</summary>
    public long RevisionNumber => Tables.SemanticRevisionNumber;

    /// <summary>The snapshot commands with every kind's copier and remover, as the API registers them.</summary>
    /// <param name="rules">The version rules restore runs.</param>
    /// <param name="copiers">Replaces the copiers of the kinds it names.</param>
    /// <returns>The commands.</returns>
    public SnapshotCommands Commands(IEnumerable<IVersionRules> rules, params IReadOnlyList<IVersionCopier> copiers)
    {
        var replaced = copiers.Select(copier => copier.Kind).ToHashSet();
        return new SnapshotCommands(
            Tables.Snapshots, Restores, Tables.DatabaseInfo, Index(), Live(), [.. copiers, .. Copiers().Where(copier => !replaced.Contains(copier.Kind))],
            Removers(), rules, Revision(), new TenantInfoReader(Tables.DatabaseInfo), _ids, _time);
    }

    /// <summary>The snapshot reads with every kind's copier, as the API registers them.</summary>
    /// <returns>The reads.</returns>
    public SnapshotQueries Queries()
    {
        return new SnapshotQueries(Tables.Snapshots, Restores, Index(), Live(), Copiers());
    }

    /// <summary>Every kind's copier.</summary>
    /// <returns>The copiers, in capture order.</returns>
    public IReadOnlyList<IVersionCopier> Copiers()
    {
        var t = Tables;
        return
        [
            new DatasetVersionCopier(t.DatasetVersions, DatasetContent(), _ids, _time),
            new DimensionVersionCopier(t.DimensionVersions, t.Levels, t.Members, Bands, new DimensionVersionCloner(t.Levels, t.Members, Bands, t.Closure, _ids), DimensionRemover(), _ids, _time),
            new MappingVersionCopier(MappingVersions, Entries, _ids, _time),
            new BridgeVersionCopier(t.BridgeVersions, t.BridgeMappings, new BridgeStore(t.Bridges, t.BridgeVersions, t.BridgeMappings, Live(), _ids, _time), _ids, _time),
            new CalculationModelVersionCopier(t.ModelVersions, t.Variables, t.CalculatedMeasures, t.Parameters, Attributions, AttributionDrivers, ModelRemover(), _ids, _time),
            new ReportVersionCopier(
                t.ReportVersions, t.Lines, t.Columns, t.MemberOptions, t.RowParameters, t.Styles, t.Scopes, t.Scenarios, t.Sources, t.SourceVariables, t.LeftOut,
                t.Profiles, t.Picks, t.CalculationDimensions, ReportRemover(), _ids, _time)
        ];
    }

    /// <summary>The semantic revision.</summary>
    /// <returns>The revision service.</returns>
    public SemanticRevision Revision()
    {
        return new SemanticRevision(Tables.DatabaseInfo, _time);
    }

    /// <summary>The ids of the version rows a snapshot holds, kind by kind.</summary>
    /// <param name="snapshotId">The snapshot.</param>
    /// <returns>The version ids.</returns>
    public IReadOnlyList<long> VersionIds(long snapshotId)
    {
        var t = Tables;
        return
        [
            .. t.DatasetVersions.All.Where(row => row.SnapshotId == snapshotId).Select(row => row.DatasetVersionId),
            .. t.DimensionVersions.All.Where(row => row.SnapshotId == snapshotId).Select(row => row.DimensionVersionId),
            .. MappingVersions.All.Where(row => row.SnapshotId == snapshotId).Select(row => row.MappingVersionId),
            .. t.BridgeVersions.All.Where(row => row.SnapshotId == snapshotId).Select(row => row.BridgeVersionId),
            .. t.ModelVersions.All.Where(row => row.SnapshotId == snapshotId).Select(row => row.CalculationModelVersionId),
            .. t.ReportVersions.All.Where(row => row.SnapshotId == snapshotId).Select(row => row.ReportVersionId)
        ];
    }

    /// <summary>The index of every snapshot's versions.</summary>
    private SnapshotIndex Index()
    {
        var t = Tables;
        return new SnapshotIndex(
            t.Datasets, t.DatasetVersions, t.Dimensions, t.DimensionVersions, MappingSets, MappingVersions, t.Bridges, t.BridgeVersions, t.Models, t.ModelVersions,
            t.Reports, t.ReportVersions, t.Sources);
    }

    /// <summary>Every kind's remover.</summary>
    private IReadOnlyList<IVersionRemover> Removers()
    {
        var t = Tables;
        return
        [
            new DatasetVersionRemover(t.Datasets, t.DatasetVersions, DatasetContent()),
            DimensionRemover(),
            new MappingVersionRemover(MappingSets, MappingVersions, Entries),
            new BridgeVersionRemover(t.Bridges, t.BridgeVersions, t.BridgeMappings),
            ModelRemover(),
            ReportRemover()
        ];
    }

    /// <summary>The dataset version content over the tables.</summary>
    private DatasetVersionContentStore DatasetContent()
    {
        var t = Tables;
        return new DatasetVersionContentStore(t.Fields, Grains, GrainFields, t.Bindings, t.TimeBindings, t.Measures, Components, Dependencies, TimePolicies);
    }

    /// <summary>The dimension remover.</summary>
    private DimensionVersionRemover DimensionRemover()
    {
        var t = Tables;
        return new DimensionVersionRemover(t.Dimensions, t.Roles, t.DimensionVersions, t.Levels, t.Members, t.Closure, Bands);
    }

    /// <summary>The calculation model remover.</summary>
    private CalculationModelVersionRemover ModelRemover()
    {
        var t = Tables;
        return new CalculationModelVersionRemover(t.Models, t.ModelVersions, t.Variables, t.CalculatedMeasures, t.Parameters, Attributions, AttributionDrivers);
    }

    /// <summary>The report remover.</summary>
    private ReportVersionRemover ReportRemover()
    {
        var t = Tables;
        return new ReportVersionRemover(
            t.Reports, t.ReportVersions, t.Profiles, t.Picks, t.SourceVariables, t.LeftOut, t.Sources, t.CalculationDimensions, t.Scopes, t.Scenarios, t.RowParameters,
            t.Lines, t.Columns, t.MemberOptions, t.Styles);
    }

    /// <summary>The live snapshot over the snapshot table.</summary>
    private LiveSnapshot Live()
    {
        return new LiveSnapshot(Tables.Snapshots);
    }
}
