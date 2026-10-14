using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms <c>mapped_bindings_resolvable</c>: a mapped binding of a pinned dataset version is reported when its mapping
/// set has no published mapping version, or, for a binding that pins a dimension version, none for that version; other
/// binding kinds are not checked. The engine maps source keys through the published mapping version of the binding's
/// mapping set and resolved dimension version, so a release without one fails queries that group by the binding.
/// </summary>
public sealed class MappedBindingsResolvableRuleTests
{
    /// <summary>The pinned dataset version.</summary>
    private const long DatasetVersionId = 11;

    /// <summary>The mapping set of the mapped binding.</summary>
    private const long MappingSetId = 61;

    /// <summary>The dimension version a published mapping version targets.</summary>
    private const long MappedDimensionVersionId = 41;

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
        var violations = await EvaluateAsync([MappedBinding()], [MappingVersion(71, MappedDimensionVersionId, PublicationStatuses.Draft)]);

        var violation = Assert.Single(violations);
        Assert.Equal("mapped_bindings_resolvable", violation.Rule);
        Assert.Equal("Binding 'Branch' of dataset 'loans' maps through mapping set 'core_branch', which has no published mapping version.", violation.Message);
    }

    /// <summary>A published mapping version of the set passes, and a direct binding without a mapping set is not checked.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PublishedMappingVersion_Passes()
    {
        var direct = Binding(32, DatasetVersionId, "Product", 200, BindingKinds.Direct);

        Assert.Empty(await EvaluateAsync([MappedBinding(), direct], [MappingVersion(72, MappedDimensionVersionId, PublicationStatuses.Published)]));
    }

    /// <summary>A binding that pins a dimension version needs a published mapping version for that version, not just for any version.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PinnedBinding_NeedsAMappingVersionForItsPinnedVersion()
    {
        var pinned = MappedBinding() with { PinnedDimensionVersionId = 42 };

        var violation = Assert.Single(await EvaluateAsync([pinned], [MappingVersion(72, MappedDimensionVersionId, PublicationStatuses.Published)]));

        Assert.Equal(
            "Binding 'Branch' of dataset 'loans' maps through mapping set 'core_branch', which has no published mapping version for its pinned dimension version 42.",
            violation.Message);
    }

    /// <summary>The unpinned mapped binding of loans v1.</summary>
    private static DimensionBindingRecord MappedBinding()
    {
        return Binding(31, DatasetVersionId, "Branch", 100, BindingKinds.Mapped) with { MappingSetId = MappingSetId };
    }

    /// <summary>A version of the mapping set that targets a dimension version.</summary>
    private static MappingVersionRecord MappingVersion(long mappingVersionId, long dimensionVersionId, string status)
    {
        return new MappingVersionRecord
        {
            MappingVersionId = mappingVersionId,
            MappingSetId = MappingSetId,
            VersionNumber = 1,
            DimensionVersionId = dimensionVersionId,
            Status = status
        };
    }

    /// <summary>Evaluates the rule with the given bindings of the pinned version and mapping versions of the set.</summary>
    private async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DimensionBindingRecord[] bindings, MappingVersionRecord[] mappingVersions)
    {
        var reader = new ReleaseManifestReader(_manifest, _datasets, new InMemoryTableGateway<DimensionBindingRecord>(bindings));
        var rule = new MappedBindingsResolvableRule(reader, new InMemoryTableGateway<MappingVersionRecord>(mappingVersions), _mappingSets);
        return await rule.EvaluateAsync(ReleaseId, TestContext.Current.CancellationToken);
    }
}
