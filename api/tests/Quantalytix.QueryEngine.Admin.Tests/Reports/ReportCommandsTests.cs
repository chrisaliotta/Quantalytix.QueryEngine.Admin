using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the report lifecycle commands (blueprint 16): a new report starts disabled with its own published report dimension (P1,
/// P3), or as a copy of another report with bridges of its own (OI-47); enabling runs every report rule (P2); and a delete takes the
/// report's dimension, bridges, content and pins with it, never blocked (P12).
/// </summary>
public sealed class ReportCommandsTests
{
    /// <summary>The seeded report.</summary>
    private const long ReportId = ReportRows.ReportId;

    /// <summary>The tables and the module.</summary>
    private readonly ReportFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A new report starts disabled with a single root row in its own report dimension, published and pinned.</summary>
    /// <returns>A task that completes when the report is checked.</returns>
    [Fact]
    public async Task Create_StartsDisabledWithItsOwnPublishedDimension()
    {
        var created = (await _fixture.Commands().CreateAsync(Input("income"), Token)).Value!;

        var document = await _fixture.Queries().DocumentAsync(created.Id, Token);
        Assert.False(created.IsEnabled);
        Assert.Equal(("income", "month"), (created.Key, created.Periodicity));
        Assert.Equal(["total"], document.Rows.Select(row => row.Key));
        var dimension = _fixture.Dimensions.All.Single(row => row.DimensionId == document.ReportDimensionId);
        Assert.Equal(("income", DimensionPurposes.Report), (dimension.DimensionKey, dimension.DimensionPurpose));
        Assert.Contains(_fixture.DimensionPins.All, pin => pin.DimensionId == document.ReportDimensionId);
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
        await _fixture.Columns.DeleteAsync(new { ReportId }, Token);

        var refusal = (await _fixture.Commands().SetEnabledAsync(ReportId, new ReportEnabledInput { IsEnabled = true }, Token)).Refusal!;

        Assert.Equal(ColumnKeysDistinctRule.Code, Assert.Single(refusal.Violations).Code);
        Assert.False(_fixture.Reports.All.Single().IsEnabled);
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

    /// <summary>A delete takes the report's content, its source's bridge and its dimension with their pins; nothing blocks it.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task Delete_RemovesEverythingTheReportOwns()
    {
        await _fixture.Remover().DeleteAsync(ReportId, Token);

        Assert.Empty(_fixture.Reports.All);
        Assert.Empty(_fixture.Sources.All);
        Assert.Empty(_fixture.Profiles.All);
        Assert.Empty(_fixture.Columns.All);
        Assert.DoesNotContain(_fixture.Bridges.All, bridge => bridge.BridgeId == BridgeId);
        Assert.DoesNotContain(_fixture.Dimensions.All, dimension => dimension.DimensionId == ReportRows.ReportDimensionId);
        Assert.DoesNotContain(_fixture.DimensionPins.All, pin => pin.DimensionId == ReportRows.ReportDimensionId);
        Assert.Empty(_fixture.BridgePins.All);
    }

    /// <summary>A new report over model 40.</summary>
    private static ReportCreateInput Input(string key)
    {
        return new ReportCreateInput { Key = key, DisplayName = key, ReportType = "financial", ModelId = CalculationModels.ModelRows.CalculationModelId };
    }
}
