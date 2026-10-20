using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>report_sources_resolve</c>: a source that reads a dataset the release does not pin is reported; a direct or mapped
/// classification's field must be an enabled field of the pinned dataset version, and a bridge classification's role must have an
/// enabled binding into members there; and its time field, measure and filter fields must each resolve. The engine trusts these
/// keys. The rule runs in two scopes: a release checks every published report, and a report version checks itself against the
/// active release.
/// </summary>
public sealed class ReportSourcesResolveRuleTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>The seeded GL source resolves.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ResolvingSource_Passes()
    {
        Assert.Empty(await EvaluateAsync(Rule(VersionKinds.SemanticRelease), ReleaseId));
    }

    /// <summary>A source of the loans dataset, which the release does not pin, is reported for the release and for its report version.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SourceOfAnUnpinnedDataset_IsReported()
    {
        await _fixture.Sources.InsertAsync(Source(72, "balance", "loan_balance", LoansDatasetId), TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(Rule(VersionKinds.SemanticRelease), ReleaseId));

        Assert.Equal(ReportSourcesResolveRule.Code, violation.Rule);
        Assert.Equal("Report 'balance_sheet' source 'loan_balance' reads dataset 'loans', which the release does not pin.", violation.Message);
        Assert.Equal(violation.Message, Assert.Single(await EvaluateAsync(Rule(VersionKinds.ReportVersion), ReportVersionId)).Message);
    }

    /// <summary>A classification by a field the dataset version lacks is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ClassificationByAMissingField_IsReported()
    {
        await _fixture.Sources.InsertAsync(Source(71, "balance", "gl_balance", GlDatasetId) with { ClassificationFieldKey = "account" }, TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(Rule(VersionKinds.SemanticRelease), ReleaseId));

        Assert.Equal("Report 'balance_sheet' source 'gl_balance' classifies by 'account', which is no enabled field of 'gl'.", violation.Message);
    }

    /// <summary>
    /// A bridge classification starts from the members its role's binding classifies: the bridge from accounts reads the account
    /// role, which GL binds directly, and is reported once that binding is disabled, because the engine would classify nothing.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BridgeClassification_NeedsAnEnabledMemberBindingOfItsRole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Sources.InsertAsync(
            Source(72, "balance", "gl_bridged", GlDatasetId) with
            {
                ClassificationKind = ClassificationKinds.Bridge,
                ClassificationFieldKey = null,
                ClassificationRoleId = AccountRoleId,
                BridgeId = AccountBridgeId
            },
            cancellationToken);
        Assert.Empty(await EvaluateAsync(Rule(VersionKinds.SemanticRelease), ReleaseId));

        var binding = await _fixture.Bindings.GetAsync(new { BindingId = 31L }, cancellationToken);
        await _fixture.Bindings.UpdateAsync(binding with { IsEnabled = false }, cancellationToken);
        var violation = Assert.Single(await EvaluateAsync(Rule(VersionKinds.SemanticRelease), ReleaseId));

        Assert.Equal(
            "Report 'balance_sheet' source 'gl_bridged' bridges from role 'gl_account', which 'gl' binds into no members with an enabled binding.",
            violation.Message);
    }

    /// <summary>A time field without a time binding, a measure GL lacks and a filter on a field that is not filterable are each reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task UnresolvedTimeFieldMeasureAndFilter_AreEachReported()
    {
        const string memoFilter = """{"operator":"eq","left":{"type":"field","id":"memo"},"right":{"type":"literal","value":"x"}}""";
        var source = Source(71, "balance", "gl_balance", GlDatasetId) with { TimeFieldKey = "region", MeasureKey = "income", SourceFilterJson = memoFilter };
        await _fixture.Sources.InsertAsync(source, TestContext.Current.CancellationToken);

        var violations = await EvaluateAsync(Rule(VersionKinds.SemanticRelease), ReleaseId);

        Assert.Equal(
            ["Report 'balance_sheet' source 'gl_balance': time field 'region' of 'gl' has no time binding.",
             "Report 'balance_sheet' source 'gl_balance': measure 'income' is no enabled measure of 'gl'.",
             "Report 'balance_sheet' source 'gl_balance': its filter compares 'memo', which is no enabled, filterable field of 'gl'."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>The rule for a kind, over the fixture's tables.</summary>
    private ReportSourcesResolveRule Rule(string kind)
    {
        return new ReportSourcesResolveRule(kind, _fixture.Reader());
    }
}
