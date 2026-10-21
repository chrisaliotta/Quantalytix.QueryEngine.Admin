using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms that a mapping set a binding classifies through cannot be deleted: the binding names it by ID, so deleting it would leave
/// the binding classifying through nothing. The detail names the binding by its role's key.
/// </summary>
public sealed class MappingSetDeleteTests
{
    /// <summary>The branch dimension the mapping set maps into.</summary>
    private const long BranchId = 600;

    /// <summary>Mapping set 62, which the branch binding classifies through, is not deleted.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task MappingSetABindingClassifiesThrough_IsNotDeleted()
    {
        var mappingSets = new InMemoryTableGateway<MappingSetRecord>(MappingRows.MappingSet(62, BranchId));
        var versions = new InMemoryTableGateway<MappingVersionRecord>();
        var roles = new InMemoryTableGateway<DimensionRoleRecord>(DimensionRows.Role(81, "branch", BranchId));
        var ids = new SequentialIdGenerator(1000);
        var service = new MappingSetService(
            mappingSets,
            versions,
            new InMemoryTableGateway<DimensionBindingRecord>(DatasetRows.Binding(21, 11, 81, BindingKinds.Mapped, 1) with { MappingSetId = 62 }),
            new BindingNames(roles, new InMemoryTableGateway<FieldRecord>()),
            new MappingVersionService(versions, new InMemoryTableGateway<DimensionMappingRecord>(), new MappingVersionCloner(new InMemoryTableGateway<DimensionMappingRecord>(), ids), ids),
            new SemanticRevision(new InMemoryTableGateway<DatabaseInfoRecord>(), new FakeTimeProvider()),
            ids);

        var detail = await service.DeleteAsync(62, TestContext.Current.CancellationToken);

        Assert.Equal("Mapping set 'zz_map_codes_62' cannot be deleted: referenced by bindings 'branch'.", detail);
        Assert.Single(mappingSets.All);
    }
}
