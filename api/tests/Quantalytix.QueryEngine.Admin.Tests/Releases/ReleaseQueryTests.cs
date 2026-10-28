using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms the derived release read models (OI-38): a release diff names the enabled reports that read each dataset going away, so
/// the activate dialog warns before reports lose their data; the list carries each release's difference from the active one; and
/// report reads name the analytical identities a report resolves through in the active release, which set readiness checks.
/// </summary>
public sealed class ReleaseQueryTests
{
    /// <summary>Going from the active release to the empty draft removes GL, which the balance sheet reads.</summary>
    /// <returns>A task that completes when the diff is checked.</returns>
    [Fact]
    public async Task Diff_NamesTheReportsThatReadARemovedDataset()
    {
        var fixture = await CreateAsync();

        var diff = (await fixture.Queries().DiffAsync(SemanticReleaseId, DraftReleaseId, TestContext.Current.CancellationToken))!;

        var removed = Assert.Single(diff.Removed);
        Assert.Equal((GlDatasetId, GlVersionId, "balance_sheet"), (removed.DatasetId, removed.VersionId, Assert.Single(removed.ReadBy)));
        Assert.Null(await fixture.Queries().DiffAsync(SemanticReleaseId, 4242, TestContext.Current.CancellationToken));
    }

    /// <summary>Every release but the active one carries its difference from it; the active one has none.</summary>
    /// <returns>A task that completes when the list is checked.</returns>
    [Fact]
    public async Task List_DiffsEveryOtherReleaseFromTheActiveOne()
    {
        var fixture = await CreateAsync();

        var items = await fixture.Queries().ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([SemanticReleaseId, DraftReleaseId, PreviousReleaseId], items.Select(item => item.Id).Order());
        Assert.Null(items.Single(item => item.IsActive).DiffVsActive);
        Assert.Equal(1, items.Single(item => item.Id == PreviousReleaseId).DiffVsActive!.Unchanged);
    }

    /// <summary>
    /// The balance sheet reads the region dimension through its calculation dimension and GL's region binding, and the account
    /// dimension through GL's account binding; GL's time binding resolves through nothing.
    /// </summary>
    /// <returns>A task that completes when the reads are checked.</returns>
    [Fact]
    public async Task ReportReads_NameTheAnalyticalIdentities()
    {
        var fixture = await CreateAsync();

        var report = Assert.Single(await fixture.Queries().ReportReadsAsync(TestContext.Current.CancellationToken));

        Assert.Equal([(PinKinds.Dimension, RegionDimensionId), (PinKinds.Dimension, AccountDimensionId)], report.Reads.Select(read => (read.Kind, read.IdentityId)));
        Assert.Equal(("gl", GlDatasetId, false, "balance"), (report.Sources[0].Key, report.Sources[0].DatasetId, report.Sources[0].UsesFactors, Assert.Single(report.Sources[0].MeasureKeys)));
    }
}
