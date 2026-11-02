using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Snapshots;

/// <summary>
/// Confirms the snapshot commands the versioning model rests on (blueprint 14): a capture copies every live version of every kind
/// into a new frozen snapshot with its content and leaves live as it was, so edits after it never reach the snapshot; a capture
/// during which live changed is refused and leaves nothing behind, because it may hold half a commit; and a restore makes live equal
/// the snapshot, repairing edited versions in place, bringing back identities live lost and deleting identities added since, but
/// only once every version passes its kind's rules.
/// </summary>
public sealed class SnapshotCommandsTests
{
    /// <summary>The tenant and the commands.</summary>
    private readonly SnapshotFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A capture holds a copy of each of the six kinds' live versions under new ids, with copies of their content rows; the live
    /// versions keep their ids and rows, and the revision advances once.
    /// </summary>
    /// <returns>A task that completes when the capture is checked.</returns>
    [Fact]
    public async Task Capture_CopiesEveryKindAndLeavesLiveUntouched()
    {
        var live = _fixture.VersionIds(SnapshotRows.LiveId);
        var content = ContentCounts();

        var outcome = await _fixture.Commands(PassingRules()).CaptureAsync(Input("fy26_close"), "tester", Token);

        Assert.True(outcome.Succeeded);
        Assert.False(outcome.Value.IsLive);
        Assert.Equal(7, live.Count);
        Assert.Equal(live, _fixture.VersionIds(SnapshotRows.LiveId));
        var frozen = _fixture.VersionIds(outcome.Value.Id);
        Assert.Equal(live.Count, frozen.Count);
        Assert.Empty(frozen.Intersect(live));
        Assert.Equal(content.Select(count => count * 2), ContentCounts());
        Assert.Equal(ReportFixture.StartingRevision + 1, _fixture.RevisionNumber);
    }

    /// <summary>
    /// When a commit advances the revision while the versions are copied, the capture is refused as a conflict: no snapshot row is
    /// written and every copy is deleted again, so no request can name a snapshot that holds half a commit.
    /// </summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task Capture_IsRefusedWhenLiveChangesDuringIt()
    {
        var live = _fixture.VersionIds(SnapshotRows.LiveId);
        var content = ContentCounts();
        var committing = new CommittingCopier(_fixture.Copiers()[0], _fixture.Revision());

        var outcome = await _fixture.Commands(PassingRules(), committing).CaptureAsync(Input("fy26_close"), "tester", Token);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
        Assert.Equal([SnapshotRows.LiveId], _fixture.Tables.Snapshots.All.Select(row => row.SnapshotId));
        Assert.Equal(live, _fixture.VersionIds(SnapshotRows.LiveId));
        Assert.Equal(content, ContentCounts());
    }

    /// <summary>
    /// After a capture, live renames a report column, loses the mapping set's version and gains a new dimension. Restoring the
    /// capture puts the column back in the report's live version, which keeps its id; gives the mapping set a live version with the
    /// captured entry again; deletes the new dimension, which no snapshot holds, with its version; logs the restore and advances the
    /// revision.
    /// </summary>
    /// <returns>A task that completes when the restore is checked.</returns>
    [Fact]
    public async Task Restore_MakesLiveEqualTheSnapshot()
    {
        var frozenId = (await _fixture.Commands(PassingRules()).CaptureAsync(Input("fy26_close"), "tester", Token)).Value!.Id;
        var column = _fixture.Tables.Columns.All.Single(row => row.ReportVersionId == ReportRows.ReportVersionId);
        await _fixture.Tables.Columns.UpdateAsync(column with { DisplayName = "Renamed" }, Token);
        await _fixture.Entries.DeleteAsync(new { SnapshotFixture.MappingVersionId }, Token);
        await _fixture.MappingVersions.DeleteAsync(new { SnapshotFixture.MappingVersionId }, Token);
        await _fixture.Tables.Dimensions.InsertAsync(DimensionRows.Dimension(900, DimensionKinds.Categorical), Token);
        await _fixture.Tables.DimensionVersions.InsertAsync(DimensionRows.Version(901, 900), Token);
        await _fixture.Tables.Members.InsertAsync(DimensionRows.Member(902, 901, "NEW"), Token);
        var revision = _fixture.RevisionNumber;

        var outcome = await _fixture.Commands(PassingRules()).RestoreAsync(frozenId, "tester", Token);

        Assert.True(outcome.Succeeded);
        var liveReport = _fixture.Tables.ReportVersions.All.Single(row => row.SnapshotId == SnapshotRows.LiveId);
        Assert.Equal(ReportRows.ReportVersionId, liveReport.ReportVersionId);
        Assert.Equal(column.DisplayName, _fixture.Tables.Columns.All.Single(row => row.ReportVersionId == liveReport.ReportVersionId).DisplayName);
        var liveMapping = _fixture.MappingVersions.All.Single(row => row.SnapshotId == SnapshotRows.LiveId);
        Assert.Equal(SnapshotFixture.MappingSetId, liveMapping.MappingSetId);
        Assert.Equal([("06", "1001")], _fixture.Entries.All.Where(row => row.MappingVersionId == liveMapping.MappingVersionId).Select(row => (row.SourceKeyValue, row.MemberKey)));
        Assert.DoesNotContain(_fixture.Tables.Dimensions.All, row => row.DimensionId == 900);
        Assert.DoesNotContain(_fixture.Tables.DimensionVersions.All, row => row.DimensionId == 900);
        Assert.DoesNotContain(_fixture.Tables.Members.All, row => row.DimensionVersionId == 901);
        Assert.Equal(_fixture.VersionIds(frozenId).Count, _fixture.VersionIds(SnapshotRows.LiveId).Count);
        Assert.Equal([frozenId], _fixture.Restores.All.Select(row => row.SnapshotId));
        Assert.Equal(revision + 1, _fixture.RevisionNumber);
    }

    /// <summary>
    /// A snapshot whose versions break their kind's rules against the database as it is now is not restored: the violations are
    /// answered and live is left exactly as it was.
    /// </summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task Restore_IsRefusedWhenAVersionBreaksItsRules()
    {
        var frozenId = (await _fixture.Commands(PassingRules()).CaptureAsync(Input("fy26_close"), "tester", Token)).Value!.Id;
        await _fixture.Tables.Dimensions.InsertAsync(DimensionRows.Dimension(900, DimensionKinds.Categorical), Token);
        await _fixture.Tables.DimensionVersions.InsertAsync(DimensionRows.Version(901, 900), Token);
        var live = _fixture.VersionIds(SnapshotRows.LiveId);
        var violation = new RuleViolation { Code = "dataset_table_exists", Message = "Table 'gl_balances' no longer exists." };
        IVersionRules[] rules = [.. PassingRules().Where(rule => rule.Kind != SnapshotKinds.Dataset), new FixedRules(SnapshotKinds.Dataset, [violation])];

        var outcome = await _fixture.Commands(rules).RestoreAsync(frozenId, "tester", Token);

        Assert.Equal(RefusalKind.RulesViolated, outcome.Refusal!.Kind);
        Assert.Equal([violation], outcome.Refusal.Violations);
        Assert.Equal(live, _fixture.VersionIds(SnapshotRows.LiveId));
        Assert.Empty(_fixture.Restores.All);
    }

    /// <summary>A capture of the tenant under a key.</summary>
    private static CaptureSnapshotInput Input(string key)
    {
        return new CaptureSnapshotInput { Key = key, DisplayName = key, Description = null };
    }

    /// <summary>Rules every kind's versions pass.</summary>
    private static IVersionRules[] PassingRules()
    {
        return [.. SnapshotKinds.All.Select(kind => new FixedRules(kind, []))];
    }

    /// <summary>The row counts of content tables of every kind, which a capture doubles.</summary>
    private int[] ContentCounts()
    {
        var t = _fixture.Tables;
        return
        [
            t.Fields.All.Count, t.Bindings.All.Count, t.Measures.All.Count, t.Members.All.Count, _fixture.Entries.All.Count, t.BridgeMappings.All.Count,
            t.Variables.All.Count, t.Columns.All.Count, t.Sources.All.Count, t.SourceVariables.All.Count, t.Profiles.All.Count, t.Picks.All.Count
        ];
    }

    /// <summary>Version rules that answer the same violations for every version of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="violations">The violations.</param>
    private sealed class FixedRules(string kind, IReadOnlyList<RuleViolation> violations) : IVersionRules
    {
        /// <summary>The kind.</summary>
        public string Kind => kind;

        /// <summary>The fixed violations.</summary>
        /// <param name="versionId">Unused.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>The violations.</returns>
        public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(violations);
        }
    }

    /// <summary>A copier that commits to live before it copies, as an editor saving during a capture would.</summary>
    /// <param name="inner">The kind's real copier.</param>
    /// <param name="revision">The revision a commit advances.</param>
    private sealed class CommittingCopier(IVersionCopier inner, SemanticRevision revision) : IVersionCopier
    {
        /// <summary>The inner copier's kind.</summary>
        public string Kind => inner.Kind;

        /// <summary>Advances the revision, then copies.</summary>
        /// <param name="versionId">The version to copy.</param>
        /// <param name="targetSnapshotId">The snapshot the copy lands in.</param>
        /// <param name="targetVersionId">The version to replace, if any.</param>
        /// <param name="cancellationToken">Cancels the writes.</param>
        /// <returns>The copy's id.</returns>
        public async Task<long> CopyAsync(long versionId, long targetSnapshotId, long? targetVersionId, CancellationToken cancellationToken)
        {
            await revision.AdvanceAsync(cancellationToken);
            return await inner.CopyAsync(versionId, targetSnapshotId, targetVersionId, cancellationToken);
        }
    }
}
