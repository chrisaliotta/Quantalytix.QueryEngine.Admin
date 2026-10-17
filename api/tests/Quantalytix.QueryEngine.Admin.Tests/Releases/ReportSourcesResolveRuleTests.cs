using Quantalytix.QueryEngine.Admin.Application.Releases;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseReportFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms <c>report_sources_resolve</c>: a source that reads a dataset the release does not pin is reported; its report
/// field must be a classification into the report dimension pinned to one version; and its time field, measure and filter
/// fields must each resolve in the pinned dataset version. The orchestrator trusts these keys, and an unpinned report field
/// would let each request align its sources on whatever report-item version is current.
/// </summary>
public sealed class ReportSourcesResolveRuleTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReleaseReportFixture _fixture = new();

    /// <summary>The seeded GL source resolves.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ResolvingSource_Passes()
    {
        Assert.Empty(await EvaluateAsync(new ReportSourcesResolveRule(_fixture.Reader())));
    }

    /// <summary>A source of the loans dataset, which the release does not pin, is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SourceOfAnUnpinnedDataset_IsReported()
    {
        await _fixture.Sources.InsertAsync(Source(72, 51, "loan_balance", LoansDatasetId), TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(new ReportSourcesResolveRule(_fixture.Reader())));

        Assert.Equal("report_sources_resolve", violation.Rule);
        Assert.Equal("Report 'balance_sheet' source 'loan_balance' reads dataset 'loans', which the release does not pin.", violation.Message);
    }

    /// <summary>A report-item binding that leaves the version to the request time is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task UnpinnedReportField_IsReported()
    {
        var binding = await _fixture.Bindings.GetAsync(new { BindingId = 31L }, TestContext.Current.CancellationToken);
        await _fixture.Bindings.InsertAsync(binding with { PinnedDimensionVersionId = null }, TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(new ReportSourcesResolveRule(_fixture.Reader())));

        Assert.Equal(
            "Report 'balance_sheet' source 'gl_balance': report field 'gl_account' of 'gl' is no member-grouped, version-pinned classification into the report dimension.",
            violation.Message);
    }

    /// <summary>A time field without a time binding, a measure GL lacks and a filter on a field that is not filterable are each reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task UnresolvedTimeFieldMeasureAndFilter_AreEachReported()
    {
        const string memoFilter = """{"operator":"eq","left":{"type":"field","id":"memo"},"right":{"type":"literal","value":"x"}}""";
        var source = Source(71, 51, "gl_balance", GlDatasetId) with { TimeFieldKey = "region", MeasureKey = "income", SourceFilterJson = memoFilter };
        await _fixture.Sources.InsertAsync(source, TestContext.Current.CancellationToken);

        var violations = await EvaluateAsync(new ReportSourcesResolveRule(_fixture.Reader()));

        Assert.Equal(
            ["Report 'balance_sheet' source 'gl_balance': time field 'region' of 'gl' has no time binding.",
             "Report 'balance_sheet' source 'gl_balance': measure 'income' is no enabled measure of 'gl'.",
             "Report 'balance_sheet' source 'gl_balance': its filter compares 'memo', which is no enabled, filterable field of 'gl'."],
            violations.Select(violation => violation.Message));
    }
}
