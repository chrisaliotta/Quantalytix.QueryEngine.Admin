using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms the report rules over a dataset commit: the engine serves every enabled report at once on the version a commit leaves,
/// so a dataset commit that takes away a key an enabled report's source reads is refused at the commit stage, with the finding
/// naming the report and pointing at the dataset's settings tab.
/// </summary>
public sealed class DatasetReportsResolveRuleTests
{
    /// <summary>The report graph fixture: enabled report <c>balance_sheet</c>, whose source <c>gl</c> maps rows by GL's <c>gl_account</c>.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>
    /// GL's live version as stored passes; renaming its <c>gl_account</c> field breaks the source's row key, which the commit-stage
    /// rule reports for the report.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task CommitBreakingAReportSourceKey_IsRefused()
    {
        var rule = new DatasetReportsResolveRule(_fixture.Reader(), [new ReportSourcesResolveRule(), new ReportFactorsApplyRule()]);
        var token = TestContext.Current.CancellationToken;

        var stored = await rule.EvaluateAsync(GlVersion(field => field), token);
        var violation = Assert.Single(await rule.EvaluateAsync(GlVersion(field => field.FieldKey == "gl_account" ? field with { FieldKey = "account" } : field), token));

        Assert.Equal(RuleStage.Commit, rule.Stage);
        Assert.Empty(stored);
        Assert.Equal(ReportSourcesResolveRule.Code, violation.Code);
        Assert.Equal("Report 'balance_sheet' source 'gl' maps rows by 'gl_account', which is no enabled field of 'gl'.", violation.Message);
        Assert.Equal(new ViolationTarget { Area = DatasetTabs.Settings, ItemKey = "balance_sheet" }, violation.Target);
    }

    /// <summary>GL's live version as a pending change set would leave it, with each field passed through an edit.</summary>
    private DatasetSnapshot GlVersion(Func<FieldRecord, FieldRecord> edit)
    {
        return DatasetRows.Snapshot(
            fields: [.. _fixture.Fields.All.Where(field => field.DatasetVersionId == GlVersionId).Select(edit)],
            bindings: [.. _fixture.Bindings.All],
            timeBindings: [.. _fixture.TimeBindings.All],
            measures: [.. _fixture.Measures.All]) with
        {
            Dataset = _fixture.Datasets.All.Single(dataset => dataset.DatasetId == GlDatasetId),
            Version = _fixture.DatasetVersions.All.Single()
        };
    }
}
