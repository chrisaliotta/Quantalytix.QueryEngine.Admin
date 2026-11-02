using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the report lifecycle commands (blueprint 16): a new report starts disabled with its own live report dimension (P1, P3),
/// or as a copy of another report with bridges of its own (OI-47); enabling runs every report rule (P2); and a delete takes the
/// report's dimension, bridges and content with it, blocked only by a frozen snapshot that holds the report (P12).
/// </summary>
public sealed class ReportCommandsTests
{
    /// <summary>The seeded report.</summary>
    private const long ReportId = ReportRows.ReportId;

    /// <summary>The tables and the module.</summary>
    private readonly ReportFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A new report starts disabled with a single root row in its own report dimension, which the live snapshot holds.</summary>
    /// <returns>A task that completes when the report is checked.</returns>
    [Fact]
    public async Task Create_StartsDisabledWithItsOwnLiveDimension()
    {
        var created = (await _fixture.Commands().CreateAsync(Input("income"), Token)).Value!;

        var document = await _fixture.Queries().DocumentAsync(created.Id, Token);
        Assert.False(created.IsEnabled);
        Assert.Equal(("income", "month"), (created.Key, created.Periodicity));
        Assert.Equal(["total"], document.Rows.Select(row => row.Key));
        var dimension = _fixture.Dimensions.All.Single(row => row.DimensionId == document.ReportDimensionId);
        Assert.Equal(("income", DimensionPurposes.Report), (dimension.DimensionKey, dimension.DimensionPurpose));
        Assert.Equal(SnapshotRows.LiveId, _fixture.DimensionVersions.All.Single(row => row.DimensionId == document.ReportDimensionId).SnapshotId);
    }

    /// <summary>A report key is unique, and the report dimension takes it, so a key either already has is a conflict.</summary>
    /// <param name="key">The taken key.</param>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Theory]
    [InlineData("balance_sheet")]
    [InlineData("account")]
    public async Task Create_WithATakenKey_IsAConflict(string key)
    {
        var refusal = (await _fixture.Commands().CreateAsync(Input(key), Token)).Refusal!;

        Assert.Equal(RefusalKind.Conflict, refusal.Kind);
    }

    /// <summary>A copy takes the other report's rows and content, starts disabled, and its bridge source gets a bridge of its own with the same rules.</summary>
    /// <returns>A task that completes when the copy is checked.</returns>
    [Fact]
    public async Task Create_CopyingAReport_CopiesItsContentWithBridgesOfItsOwn()
    {
        var original = await _fixture.Queries().DocumentAsync(ReportId, Token);

        var created = (await _fixture.Commands().CreateAsync(Input("copy") with { CopyFromReportId = ReportId }, Token)).Value!;

        var copy = await _fixture.Queries().DocumentAsync(created.Id, Token);
        Assert.False(copy.IsEnabled);
        Assert.Equal(original.Rows.Select(row => row.Key), copy.Rows.Select(row => row.Key));
        Assert.Equal(original.Columns, copy.Columns);
        Assert.Equal(original.Profiles.Single().Picks, copy.Profiles.Single().Picks);
        Assert.Equal("base", copy.Settings.DefaultProfileKey);
        var source = Assert.Single(copy.Sources);
        Assert.NotEqual(BridgeId, source.BridgeId);
        Assert.Equal(original.Sources.Single().Rules.Keys, source.Rules.Keys);
        Assert.Equal("copy_gl", _fixture.Bridges.All.Single(bridge => bridge.BridgeId == source.BridgeId).BridgeKey);
    }

    /// <summary>Enabling a report that breaks a report rule is refused with the violations, and the report stays disabled.</summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task SetEnabled_RefusesAReportThatBreaksARule()
    {
        await _fixture.Columns.DeleteAsync(new { ReportRows.ReportVersionId }, Token);

        var refusal = (await _fixture.Commands().SetEnabledAsync(ReportId, new ReportEnabledInput { IsEnabled = true }, Token)).Refusal!;

        Assert.Equal(ColumnKeysDistinctRule.Code, Assert.Single(refusal.Violations).Code);
        Assert.False(_fixture.ReportVersions.All.Single().IsEnabled);
    }

    /// <summary>Enabling a report that passes every rule offers it to the engine, so the semantic revision advances.</summary>
    /// <returns>A task that completes when the report is checked.</returns>
    [Fact]
    public async Task SetEnabled_EnablesAPassingReportAndAdvancesTheRevision()
    {
        var summary = (await _fixture.Commands().SetEnabledAsync(ReportId, new ReportEnabledInput { IsEnabled = true }, Token)).Value!;

        Assert.True(summary.IsEnabled);
        Assert.Equal(StartingRevision + 1, _fixture.SemanticRevisionNumber);
    }

    /// <summary>A delete takes the report's content, its source's bridge and its dimension, and advances the revision.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task Delete_RemovesEverythingTheReportOwns()
    {
        Assert.Null(await _fixture.Remover().DeleteAsync(ReportId, Token));

        Assert.Empty(_fixture.Reports.All);
        Assert.Empty(_fixture.ReportVersions.All);
        Assert.Empty(_fixture.Sources.All);
        Assert.Empty(_fixture.Profiles.All);
        Assert.Empty(_fixture.Columns.All);
        Assert.DoesNotContain(_fixture.Bridges.All, bridge => bridge.BridgeId == BridgeId);
        Assert.DoesNotContain(_fixture.Dimensions.All, dimension => dimension.DimensionId == ReportRows.ReportDimensionId);
        Assert.DoesNotContain(_fixture.DimensionVersions.All, version => version.DimensionId == ReportRows.ReportDimensionId);
        Assert.Equal(StartingRevision + 1, _fixture.SemanticRevisionNumber);
    }

    /// <summary>A report a frozen snapshot holds cannot be deleted, because restoring that snapshot would need it; nothing is deleted.</summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task Delete_IsBlockedByAFrozenSnapshot()
    {
        await _fixture.Snapshots.InsertAsync(SnapshotRows.Frozen(), Token);
        await _fixture.ReportVersions.InsertAsync(ReportRows.Version() with { ReportVersionId = 62, SnapshotId = SnapshotRows.FrozenId }, Token);

        var refusal = await _fixture.Remover().DeleteAsync(ReportId, Token);

        Assert.Equal([DeleteBlockerCodes.ReferencedBySnapshot], refusal!.Violations.Select(reason => reason.Code));
        Assert.Single(_fixture.Reports.All);
        Assert.NotEmpty(_fixture.Columns.All);
    }

    /// <summary>A new report over model 40.</summary>
    private static ReportCreateInput Input(string key)
    {
        return new ReportCreateInput { Key = key, DisplayName = key, ReportType = "financial", ModelId = CalculationModels.ModelRows.CalculationModelId };
    }
}
