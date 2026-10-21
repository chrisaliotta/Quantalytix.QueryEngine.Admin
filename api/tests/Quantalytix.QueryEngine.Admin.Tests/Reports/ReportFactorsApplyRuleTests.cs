using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>report_factors_apply</c>: a bridge source whose rules take items with a factor other than 1, such as -1 to take an
/// item back out of a folder's row, supplies only variables whose measures apply bridge factors. The engine multiplies by a bridge
/// factor only for those measures, so the factor would otherwise change nothing the report shows. The rule checks a report against
/// the active release, and every enabled report when a release is validated, because a new dataset version can change a measure.
/// </summary>
public sealed class ReportFactorsApplyRuleTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>GL's rule taking 1001 at -1 is reported while GL's balance ignores bridge factors, for the report and for the release.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task FactorOnAMeasureIgnoringFactors_IsReported()
    {
        await BridgeGlAsync(-1m);

        var violation = Assert.Single(await _fixture.EvaluateAsync(new ReportFactorsApplyRule()));

        Assert.Equal(ReportFactorsApplyRule.Code, violation.Rule);
        Assert.Equal(
            "Report 'balance_sheet' source 'gl' adds items into rows with factors other than 1, but variable 'balance' reads 'balance' of 'gl', which ignores bridge factors.",
            violation.Message);
        Assert.Equal(
            violation.Message,
            Assert.Single(await _fixture.EvaluateReleaseAsync(new ReportFactorsApplyRule(), ReleaseId)).Message);
    }

    /// <summary>Whole rules pass whatever the measure does, and a factor passes once the measure applies bridge factors.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task WholeRulesOrAMeasureApplyingFactors_Pass()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await BridgeGlAsync(1m);
        Assert.Empty(await _fixture.EvaluateAsync(new ReportFactorsApplyRule()));

        await _fixture.BridgeMappings.InsertAsync(BridgeRows.Mapping(AccountBridgeVersionId, "1002", "assets", 1m, 0.5m), cancellationToken);
        var balance = Assert.Single(_fixture.Measures.All);
        await _fixture.Measures.UpdateAsync(balance with { BridgeFactorBehavior = BridgeFactorBehaviors.Value }, cancellationToken);
        Assert.Empty(await _fixture.EvaluateAsync(new ReportFactorsApplyRule()));
    }

    /// <summary>Makes GL a bridge source of the account role whose bridge takes 1001 into assets with a factor.</summary>
    private async Task BridgeGlAsync(decimal factor)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Sources.InsertAsync(
            Source(GlSourceId, "gl", GlDatasetId) with
            {
                MappingMethod = ReportMappingMethods.Bridge,
                FieldKey = null,
                RoleId = AccountRoleId,
                BridgeId = AccountBridgeId
            },
            cancellationToken);
        await _fixture.BridgeMappings.InsertAsync(BridgeRows.Mapping(AccountBridgeVersionId, "1001", "assets", 1m, factor), cancellationToken);
    }
}
