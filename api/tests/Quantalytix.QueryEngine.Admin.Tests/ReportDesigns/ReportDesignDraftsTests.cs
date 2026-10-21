using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.CalculationModels;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportDesigns;

/// <summary>
/// Confirms that a report's draft carries drafts of the objects the report owns. Creating it copies the published version of the
/// report dimension, whose members are the draft's rows, and of each bridge into it, whose entries are the draft's row mappings, so
/// the design edits them without touching the published report. Deleting the draft deletes those drafts, and a bridge the draft
/// created goes with them, because no published version reads it.
/// </summary>
public sealed class ReportDesignDraftsTests
{
    /// <summary>The draft of the published design holds copies of its rows and of account 1001's mapping to LOANS.</summary>
    /// <returns>A task that completes when the draft is checked.</returns>
    [Fact]
    public async Task NewDraft_CopiesTheReportDimensionAndItsBridges()
    {
        var fixture = new ReportDesignFixture(PublicationStatuses.Published);
        var cancellationToken = TestContext.Current.CancellationToken;

        var draft = await fixture.Drafts().CreateAsync(ReportRows.ReportId, CloneOfPublished(), cancellationToken);
        var design = await fixture.Reader().ReadAsync(draft.ReportVersionId, cancellationToken);

        Assert.Equal(PublicationStatuses.Draft, design.Status);
        Assert.NotEqual(ReportDesignFixture.StructureVersionId, design.DimensionVersionId);
        Assert.Equal(["CASH", "LOANS", "TOTAL"], design.Rows.Select(row => row.Key).Order(StringComparer.Ordinal));
        Assert.Equal(["1001"], Assert.Single(design.Rows, row => row.Key == "LOANS").Mappings.Select(mapping => mapping.ItemKey));
        Assert.Equal(
            [PublicationStatuses.Draft, PublicationStatuses.Published],
            fixture.BridgeVersions.All.Select(version => version.Status).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Deleting the draft leaves only the published versions, and deletes bridge 95, whose only version is a draft the design
    /// created for a new source.
    /// </summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeletingTheDraft_DeletesItsPairedDraftsAndTheBridgesItCreated()
    {
        var fixture = new ReportDesignFixture(PublicationStatuses.Published);
        var cancellationToken = TestContext.Current.CancellationToken;
        var drafts = fixture.Drafts();
        var draft = await drafts.CreateAsync(ReportRows.ReportId, CloneOfPublished(), cancellationToken);
        await fixture.Bridges.InsertAsync(BridgeRows.Bridge(95, ReportDesignFixture.AccountDimensionId, ReportRows.ReportDimensionId), cancellationToken);
        await fixture.BridgeVersions.InsertAsync(
            BridgeRows.BridgeVersion(96, 95, 1, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Draft), cancellationToken);

        await drafts.DeleteAsync(draft.ReportVersionId, cancellationToken);

        Assert.Equal([ReportRows.ReportVersionId], fixture.ReportVersions.All.Select(version => version.ReportVersionId));
        Assert.Equal(
            [ReportDesignFixture.StructureVersionId, ReportDesignFixture.AccountVersionId],
            fixture.DimensionVersions.All.Select(version => version.DimensionVersionId).Order());
        Assert.Equal([ReportDesignFixture.BridgeVersionId], fixture.BridgeVersions.All.Select(version => version.BridgeVersionId));
        Assert.Equal([ReportDesignFixture.BridgeId], fixture.Bridges.All.Select(bridge => bridge.BridgeId));
    }

    /// <summary>A new draft over model 40 that copies the published report version.</summary>
    private static NewReportVersionInput CloneOfPublished()
    {
        return new NewReportVersionInput { ModelId = ModelRows.ModelId, SubtotalPosition = SubtotalPositions.Above, CloneFromVersionId = ReportRows.ReportVersionId };
    }
}
