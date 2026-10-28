using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms the manifest rules over a manifest held in memory, as a preview, a check and validate run them. The engine joins a
/// release's manifest on both ids and reads only published versions, so a row that pins another dataset's version or an unpublished
/// one silently drops its dataset: those two are gates that refuse the edit. An empty release and a binding whose dimension, mapping
/// set or bridge has no published version fail queries only once the release is active, so they block validate and publish but not
/// saving. Each finding names its manifest row, which the release editor shows its issue icon on.
/// </summary>
public sealed class ReleaseRuleTests
{
    /// <summary>An empty manifest is a publication finding that belongs to no row.</summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task EmptyManifest_IsAPublicationFinding()
    {
        var fixture = await CreateAsync();

        var violation = Assert.Single(await EvaluateAsync(fixture, RuleStage.Publication));

        Assert.Equal(("manifest_not_empty", (long?)null, null), (violation.Code, violation.DatasetId, violation.Target));
        Assert.Empty(await EvaluateAsync(fixture, RuleStage.Gate));
    }

    /// <summary>Pinning loans' version under GL, and loans' draft, are gates on the rows that pin them.</summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task ForeignAndUnpublishedVersions_AreGatesOnTheirRows()
    {
        var fixture = await CreateAsync();

        var violations = await EvaluateAsync(fixture, RuleStage.Gate, Row(GlDatasetId, LoansPublishedVersionId), Row(LoansDatasetId, LoansDraftVersionId));

        Assert.Equal(
            [
                ("pinned_version_belongs_to_dataset", (long?)GlDatasetId, "Dataset 'gl' pins dataset version 21, which is not one of its versions."),
                ("pinned_version_published", LoansDatasetId, "Dataset 'loans' pins v2, which is draft; a release pins only published versions.")
            ],
            violations.Select(violation => (violation.Code, violation.DatasetId, violation.Message)));
        Assert.All(violations, violation => Assert.Equal(ReleaseTargetAreas.Dataset, violation.Target!.Area));
    }

    /// <summary>
    /// A binding whose dimension has no published version is reported on its row and targets the dimension; GL's time binding of the
    /// calendar dimension, which has no version at all, passes, because the engine derives calendar periods from the bound field.
    /// </summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task BoundDimensionWithoutPublishedVersion_IsReportedOnItsRow()
    {
        var fixture = await CreateAsync();
        var region = fixture.Graph.DimensionVersions.All.Single(version => version.DimensionId == RegionDimensionId);
        await fixture.Graph.DimensionVersions.UpdateAsync(region with { Status = PublicationStatuses.Retired }, TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(fixture, RuleStage.Publication, Row(GlDatasetId, GlVersionId)));

        Assert.Equal("bound_dimensions_resolvable", violation.Code);
        Assert.Equal("Binding 'region' of dataset 'gl' binds dimension 'region', which has no published version.", violation.Message);
        Assert.Equal((GlDatasetId, "dimension", "600"), (violation.DatasetId!.Value, violation.Target!.Area, violation.Target.ItemId));
    }

    /// <summary>A mapped binding whose mapping set has only a draft is reported and targets the mapping set.</summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task MappingSetWithoutPublishedVersion_IsReported()
    {
        var fixture = await CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.MappingSets.InsertAsync(
            new MappingSetRecord { MappingSetId = 61, MappingSetKey = "core_region", SourceNamespace = "core", DimensionId = RegionDimensionId, DisplayName = "Core regions" },
            cancellationToken);
        await fixture.MappingVersions.InsertAsync(
            new MappingVersionRecord { MappingVersionId = 62, MappingSetId = 61, VersionNumber = 1, Status = PublicationStatuses.Draft, CreatedAt = DateTime.UnixEpoch },
            cancellationToken);
        var region = fixture.Graph.Bindings.All.Single(binding => binding.DimensionId == RegionDimensionId);
        await fixture.Graph.Bindings.UpdateAsync(region with { BindingKind = BindingKinds.Mapped, MappingSetId = 61 }, cancellationToken);

        var violation = Assert.Single(await EvaluateAsync(fixture, RuleStage.Publication, Row(GlDatasetId, GlVersionId)));

        Assert.Equal("Binding 'region' of dataset 'gl' maps through mapping set 'core_region', which has no published version.", violation.Message);
        Assert.Equal(("mapping_set", "61"), (violation.Target!.Area, violation.Target.ItemId));
    }

    /// <summary>A manifest row of the draft release.</summary>
    private static ReleaseDatasetRecord Row(long datasetId, long datasetVersionId)
    {
        return new ReleaseDatasetRecord { SemanticReleaseId = DraftReleaseId, DatasetId = datasetId, DatasetVersionId = datasetVersionId };
    }

    /// <summary>Runs one stage's rules over the draft release holding the given manifest.</summary>
    private static async Task<IReadOnlyList<ReleaseViolation>> EvaluateAsync(ReleaseFixture fixture, RuleStage stage, params ReleaseDatasetRecord[] manifest)
    {
        var snapshot = await fixture.Loader().LoadAsync(DraftReleaseId, TestContext.Current.CancellationToken) with { Manifest = manifest };
        return await fixture.Rules().EvaluateAsync(snapshot, stage, TestContext.Current.CancellationToken);
    }
}
