using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms <c>mapped_bindings_resolvable</c>: a mapped binding of a pinned dataset version is reported when its mapping set
/// has no published version; other binding kinds are not checked. The active assumption set pins the mapping set's published
/// version and the engine maps source keys through it, so a release without one fails queries that group by the binding.
/// </summary>
public sealed class MappedBindingsResolvableRuleTests
{
    /// <summary>The pinned dataset version.</summary>
    private const long DatasetVersionId = 11;

    /// <summary>The mapping set of the mapped binding.</summary>
    private const long MappingSetId = 61;

    /// <summary>The release pins loans v1.</summary>
    private readonly InMemoryTableGateway<ReleaseDatasetRecord> _manifest = new(Pin(1, DatasetVersionId));

    /// <summary>The pinned dataset.</summary>
    private readonly InMemoryTableGateway<DatasetRecord> _datasets = new(Dataset(1, "loans"));

    /// <summary>The mapping set of the mapped binding.</summary>
    private readonly InMemoryTableGateway<MappingSetRecord> _mappingSets = new(new MappingSetRecord
    {
        MappingSetId = MappingSetId,
        MappingSetKey = "core_branch",
        SourceNamespace = "core",
        DimensionId = 100,
        DisplayName = "Core branch codes"
    });

    /// <summary>A mapping set whose only mapping version is a draft is reported, naming the binding, dataset and mapping set.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MappingSetWithoutPublishedVersion_IsReported()
    {
        var violations = await EvaluateAsync([MappedBinding()], [MappingVersion(71, PublicationStatuses.Draft)]);

        var violation = Assert.Single(violations);
        Assert.Equal("mapped_bindings_resolvable", violation.Code);
        Assert.Equal("Binding 'branch' of dataset 'loans' maps through mapping set 'core_branch', which has no published version.", violation.Message);
    }

    /// <summary>A published mapping version of the set passes, and a direct binding without a mapping set is not checked.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PublishedMappingVersion_Passes()
    {
        var direct = Binding(32, DatasetVersionId, ProductRoleId, 200, BindingKinds.Direct);

        Assert.Empty(await EvaluateAsync([MappedBinding(), direct], [MappingVersion(72, PublicationStatuses.Published)]));
    }

    /// <summary>The mapped binding of the branch role in loans v1.</summary>
    private static DimensionBindingRecord MappedBinding()
    {
        return Binding(31, DatasetVersionId, BranchRoleId, 100, BindingKinds.Mapped) with { MappingSetId = MappingSetId };
    }

    /// <summary>A version of the mapping set.</summary>
    private static MappingVersionRecord MappingVersion(long mappingVersionId, string status)
    {
        return new MappingVersionRecord { MappingVersionId = mappingVersionId, MappingSetId = MappingSetId, VersionNumber = 1, Status = status };
    }

    /// <summary>Evaluates the rule with the given bindings of the pinned version and mapping versions of the set.</summary>
    private async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DimensionBindingRecord[] bindings, MappingVersionRecord[] mappingVersions)
    {
        var reader = Reader(_manifest, _datasets, bindings);
        var rule = new MappedBindingsResolvableRule(reader, new InMemoryTableGateway<MappingVersionRecord>(mappingVersions), _mappingSets);
        return await rule.EvaluateAsync(SemanticReleaseId, TestContext.Current.CancellationToken);
    }
}
