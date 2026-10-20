using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms that a mapping set a binding or a report's source classifies through cannot be deleted: both name it by ID, so deleting
/// it would leave them classifying through nothing. The detail names the binding by its role's key and the source with its report
/// version.
/// </summary>
public sealed class MappingSetDeleteTests
{
    /// <summary>Mapping set 62, which the branch binding and the balance sheet's GL source classify through, is not deleted.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task MappingSetABindingOrReportClassifiesThrough_IsNotDeleted()
    {
        var mappingSets = new InMemoryTableGateway<MappingSetRecord>(MappingRows.MappingSet(62, ReportDimensionId));
        var versions = new InMemoryTableGateway<MappingVersionRecord>();
        var reportVersions = new InMemoryTableGateway<ReportVersionRecord>(ReportVersion(ReportVersionId));
        var roles = new InMemoryTableGateway<DimensionRoleRecord>(DimensionRows.Role(81, "branch", ReportDimensionId));
        var ids = new SequentialIdGenerator(1000);
        var service = new MappingSetService(
            mappingSets,
            versions,
            new InMemoryTableGateway<DimensionBindingRecord>(DatasetRows.Binding(21, 11, 81, BindingKinds.Mapped, 1) with { MappingSetId = 62 }),
            new InMemoryTableGateway<VariableSourceRecord>(Source(71, "balance", "gl_balance", 1) with { ClassificationKind = "mapped", MappingSetId = 62 }),
            new BindingNames(roles, new InMemoryTableGateway<FieldRecord>()),
            new ReportVersionNames(new InMemoryTableGateway<ReportRecord>(Report()), reportVersions),
            new MappingVersionService(versions, new InMemoryTableGateway<DimensionMappingRecord>(), new MappingVersionCloner(new InMemoryTableGateway<DimensionMappingRecord>(), ids), ids),
            new SemanticRevision(new InMemoryTableGateway<DatabaseInfoRecord>(), new FakeTimeProvider()),
            ids);

        var detail = await service.DeleteAsync(62, TestContext.Current.CancellationToken);

        Assert.Equal(
            "Mapping set 'zz_map_codes_62' cannot be deleted: referenced by bindings 'branch'; used by report sources 'gl_balance' of 'balance_sheet' v1.",
            detail);
        Assert.Single(mappingSets.All);
    }
}
