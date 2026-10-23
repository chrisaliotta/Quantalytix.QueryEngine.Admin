using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>report_sources_resolve</c>: a source that reads a dataset the release does not pin is reported; a direct source's
/// field must be an enabled field of the pinned dataset version, and a bridge source's role must have an enabled binding into
/// members there; and its time field and each supplied variable's measure and filter fields must resolve. The engine trusts these
/// keys. The rule checks a report against the active release, and every enabled report when a release is validated.
/// </summary>
public sealed class ReportSourcesResolveRuleTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>The seeded GL source resolves, in the active release and for the release being validated.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ResolvingSource_Passes()
    {
        Assert.Empty(await _fixture.EvaluateAsync(new ReportSourcesResolveRule()));
        Assert.Empty(await _fixture.EvaluateReleaseAsync(new ReportSourcesResolveRule(), SemanticReleaseId));
    }

    /// <summary>A source of the loans dataset, which the release does not pin, is reported for the release and for the report itself.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SourceOfAnUnpinnedDataset_IsReported()
    {
        await _fixture.Sources.InsertAsync(Source(72, "loans", LoansDatasetId), TestContext.Current.CancellationToken);

        var violation = Assert.Single(await _fixture.EvaluateReleaseAsync(new ReportSourcesResolveRule(), SemanticReleaseId));

        Assert.Equal(ReportSourcesResolveRule.Code, violation.Rule);
        Assert.Equal("Report 'balance_sheet' source 'loans' reads dataset 'loans', which the release does not pin.", violation.Message);
        Assert.Equal(violation.Message, Assert.Single(await _fixture.EvaluateAsync(new ReportSourcesResolveRule())).Message);
    }

    /// <summary>A direct source whose field the dataset version lacks is reported, because its rows would reach no row.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task DirectSourceByAMissingField_IsReported()
    {
        await _fixture.Sources.InsertAsync(Source(GlSourceId, "gl", GlDatasetId) with { FieldKey = "account" }, TestContext.Current.CancellationToken);

        var violation = Assert.Single(await _fixture.EvaluateAsync(new ReportSourcesResolveRule()));

        Assert.Equal("Report 'balance_sheet' source 'gl' maps rows by 'account', which is no enabled field of 'gl'.", violation.Message);
    }

    /// <summary>
    /// A bridge source starts from the members its role's binding classifies: the bridge from accounts reads the account role, which
    /// GL binds directly, and is reported once that binding is disabled, because the engine would classify nothing.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BridgeSource_NeedsAnEnabledMemberBindingOfItsRole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Sources.InsertAsync(
            Source(72, "gl_bridged", GlDatasetId) with { MappingMethod = ReportMappingMethods.Bridge, FieldKey = null, DimensionRoleId = AccountRoleId, BridgeId = AccountBridgeId },
            cancellationToken);
        Assert.Empty(await _fixture.EvaluateAsync(new ReportSourcesResolveRule()));

        var binding = await _fixture.Bindings.GetAsync(new { DimensionBindingId = 31L }, cancellationToken);
        await _fixture.Bindings.UpdateAsync(binding with { IsEnabled = false }, cancellationToken);
        var violation = Assert.Single(await _fixture.EvaluateAsync(new ReportSourcesResolveRule()));

        Assert.Equal(
            "Report 'balance_sheet' source 'gl_bridged' bridges from role 'gl_account', which 'gl' binds into no members with an enabled binding.",
            violation.Message);
    }

    /// <summary>A time field without a time binding, and a variable read from a measure GL lacks through a filter on a field that is not filterable, are each reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task UnresolvedTimeFieldMeasureAndFilter_AreEachReported()
    {
        const string memoFilter = """{"operator":"eq","left":{"type":"field","id":"memo"},"right":{"type":"literal","value":"x"}}""";
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Sources.InsertAsync(Source(GlSourceId, "gl", GlDatasetId) with { TimeFieldKey = "region" }, cancellationToken);
        await _fixture.SourceVariables.InsertAsync(SourceVariable(GlSourceId, "balance", "income") with { SourceFilterJson = memoFilter }, cancellationToken);

        var violations = await _fixture.EvaluateAsync(new ReportSourcesResolveRule());

        Assert.Equal(
            ["Report 'balance_sheet' source 'gl': time field 'region' of 'gl' has no time binding.",
             "Report 'balance_sheet' source 'gl': variable 'balance' reads 'income', which is no enabled measure of 'gl'.",
             "Report 'balance_sheet' source 'gl': variable 'balance' filters on 'memo', which is no enabled, filterable field of 'gl'."],
            violations.Select(violation => violation.Message));
    }
}
