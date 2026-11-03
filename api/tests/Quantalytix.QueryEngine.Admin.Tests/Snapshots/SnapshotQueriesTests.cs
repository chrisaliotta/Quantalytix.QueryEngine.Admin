using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Snapshots;

/// <summary>
/// Confirms what the snapshot reads tell a client beyond which versions a snapshot holds: a comparison flags an identity only when
/// its two versions' content differs, compared by keys, since every capture renews every surrogate id; and each entry names the
/// dimension and owning report a client routes it under, so it opens without reading the identity first.
/// </summary>
public sealed class SnapshotQueriesTests
{
    /// <summary>The tenant and the reads.</summary>
    private readonly SnapshotFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Right after a capture, live and the frozen copy hold the same content under new ids, so no pair differs; once live renames a
    /// report column, only the report's pair differs.
    /// </summary>
    /// <returns>A task that completes when the comparisons are checked.</returns>
    [Fact]
    public async Task Compare_FlagsOnlyVersionsWhoseContentDiffers()
    {
        var frozenId = (await _fixture.Commands([]).CaptureAsync(new CaptureSnapshotInput { Key = "fy26_close", DisplayName = "FY26", Description = null }, "tester", Token)).Value!.Id;

        var captured = await _fixture.Queries().CompareAsync(frozenId, SnapshotRows.LiveId, Token);
        var column = _fixture.Tables.Columns.All.Single(row => row.ReportVersionId == ReportRows.ReportVersionId);
        await _fixture.Tables.Columns.UpdateAsync(column with { DisplayName = "Renamed" }, Token);
        var edited = await _fixture.Queries().CompareAsync(frozenId, SnapshotRows.LiveId, Token);

        Assert.Equal(7, captured!.Common.Count);
        Assert.DoesNotContain(captured.Common, pair => pair.Differs);
        Assert.Equal([SnapshotKinds.Report], edited!.Common.Where(pair => pair.Differs).Select(pair => pair.Kind));
    }

    /// <summary>
    /// The mapping set names the account dimension it maps into; bridge 90, which report source 70 owns, names its source dimension
    /// and report 60; the report's dimension names report 60; the analytical account dimension names neither.
    /// </summary>
    /// <returns>A task that completes when the entries are checked.</returns>
    [Fact]
    public async Task Entries_NameTheirDimensionAndOwningReport()
    {
        var entries = (await _fixture.Queries().GetAsync(SnapshotRows.LiveId, Token)).Entries;

        var mappingSet = entries.Single(entry => entry.Kind == SnapshotKinds.MappingSet);
        var bridge = entries.Single(entry => entry.Kind == SnapshotKinds.Bridge);
        var reportDimension = entries.Single(entry => entry.IdentityId == ReportRows.ReportDimensionId);
        var account = entries.Single(entry => entry.IdentityId == ReportFixture.AccountDimensionId);
        Assert.Equal(ReportFixture.AccountDimensionId, mappingSet.DimensionId);
        Assert.Null(mappingSet.ReportId);
        Assert.Equal(ReportFixture.AccountDimensionId, bridge.DimensionId);
        Assert.Equal(ReportRows.ReportId, bridge.ReportId);
        Assert.Null(reportDimension.DimensionId);
        Assert.Equal(ReportRows.ReportId, reportDimension.ReportId);
        Assert.Null(account.DimensionId);
        Assert.Null(account.ReportId);
    }
}
