using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms that a mapping set a report's source or route classifies through cannot be deleted: a report names it by ID, so deleting
/// it would leave the report classifying through nothing. The detail names the source and the route with their report version.
/// </summary>
public sealed class MappingSetDeleteTests
{
    /// <summary>Mapping set 62, which the balance sheet's GL source and region route classify through, is not deleted.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task MappingSetAReportClassifiesThrough_IsNotDeleted()
    {
        var mappingSets = new InMemoryTableGateway<MappingSetRecord>(MappingRows.MappingSet(62, ReportDimensionId));
        var versions = new InMemoryTableGateway<MappingVersionRecord>();
        var reportVersions = new InMemoryTableGateway<ReportVersionRecord>(ReportVersion(ReportVersionId));
        var ids = new SequentialIdGenerator(1000);
        var service = new MappingSetService(
            mappingSets,
            versions,
            new InMemoryTableGateway<DimensionBindingRecord>(),
            new InMemoryTableGateway<VariableSourceRecord>(Source(71, "balance", "gl_balance", 1) with { ClassificationKind = "mapped", MappingSetId = 62 }),
            new InMemoryTableGateway<CalculationRouteRecord>(Route(91, 81, 1, "branch", "mapped") with { MappingSetId = 62 }),
            new ReportVersionNames(new InMemoryTableGateway<ReportRecord>(Report()), reportVersions),
            new MappingVersionService(versions, new InMemoryTableGateway<DimensionMappingRecord>(), new MappingVersionCloner(new InMemoryTableGateway<DimensionMappingRecord>(), ids), ids),
            new SemanticRevision(new InMemoryTableGateway<DatabaseInfoRecord>(), new FakeTimeProvider()),
            ids);

        var detail = await service.DeleteAsync(62, TestContext.Current.CancellationToken);

        Assert.Equal(
            "Mapping set 'zz_map_codes_62' cannot be deleted: used by report sources 'gl_balance' of 'balance_sheet' v1; used by report routes 'branch' of 'balance_sheet' v1.",
            detail);
        Assert.Single(mappingSets.All);
    }
}
