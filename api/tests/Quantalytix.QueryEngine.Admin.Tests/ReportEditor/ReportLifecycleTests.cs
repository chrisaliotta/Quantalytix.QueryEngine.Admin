using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.CalculationModels;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.ReportEditor.ReportEditorFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// Confirms how a report starts and ends. A new report is disabled, because nothing about it has been checked, and its report
/// dimension's one version is published and pinned in the active set from the start, because the editor edits it in place and every
/// run reads the active set. Deleting a report removes everything it owns, its scenarios included, with the pins of its dimension and
/// bridges, so the active set reads nothing that is gone and no scenario keeps a role from being deleted.
/// </summary>
public sealed class ReportLifecycleTests
{
    /// <summary>A new report is disabled with the default settings, over a report dimension whose published, pinned version 1 holds its total.</summary>
    /// <returns>A task that completes when the report is checked.</returns>
    [Fact]
    public async Task NewReport_IsDisabledWithAPublishedPinnedDimension()
    {
        var fixture = new ReportEditorFixture();

        var report = await fixture.ReportService().CreateAsync(NewReport(), TestContext.Current.CancellationToken);

        Assert.False(report.IsEnabled);
        Assert.Equal(ReportService.DefaultSettingsJson, report.DefaultSettingsJson);
        var dimension = Assert.Single(fixture.Dimensions.All, dimension => dimension.DimensionId == report.ReportDimensionId);
        Assert.Equal(("cash_flow", DimensionPurposes.Report), (dimension.DimensionKey, dimension.DimensionPurpose));
        var version = Assert.Single(fixture.DimensionVersions.All, version => version.DimensionId == report.ReportDimensionId);
        Assert.Equal((1, PublicationStatuses.Published), (version.VersionNumber, version.Status));
        Assert.Contains(fixture.DimensionPins.All, pin => pin.DimensionId == report.ReportDimensionId && pin.DimensionVersionId == version.DimensionVersionId);
        Assert.Equal(["total"], fixture.Members.All.Where(member => member.DimensionVersionId == version.DimensionVersionId).Select(member => member.MemberKey));
    }

    /// <summary>
    /// Deleting the report removes its content, its scenario on the account role, its source with its bridge, its report dimension,
    /// and their pins; the account dimension and its role stay.
    /// </summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeletedReport_TakesWhatItOwnsWithIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        await fixture.Scenarios.InsertAsync(ReportRows.Scenario(AccountRoleId), cancellationToken);

        await fixture.ReportService().DeleteAsync(ReportRows.ReportId, cancellationToken);

        Assert.Empty(fixture.Reports.All);
        Assert.Empty(fixture.Scenarios.All);
        Assert.Single(fixture.Roles.All);
        Assert.Empty(fixture.Sources.All);
        Assert.Empty(fixture.SourceVariables.All);
        Assert.Empty(fixture.Profiles.All);
        Assert.Empty(fixture.Picks.All);
        Assert.Empty(fixture.Columns.All);
        Assert.Empty(fixture.Bridges.All);
        Assert.Empty(fixture.BridgeMappings.All);
        Assert.Empty(fixture.BridgePins.All);
        Assert.Equal([AccountDimensionId], fixture.Dimensions.All.Select(dimension => dimension.DimensionId));
        Assert.DoesNotContain(fixture.Members.All, member => member.DimensionVersionId == StructureVersionId);
        Assert.Equal([AccountDimensionId], fixture.DimensionPins.All.Select(pin => pin.DimensionId));
    }

    /// <summary>A cash flow report over model 40.</summary>
    private static NewReportInput NewReport()
    {
        return new NewReportInput { ReportKey = "cash_flow", DisplayName = "Cash flow", ReportType = "financial", CalculationModelId = ModelRows.CalculationModelId };
    }
}
