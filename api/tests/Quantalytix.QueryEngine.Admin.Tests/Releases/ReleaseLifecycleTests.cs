using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms the strict release lifecycle (R2–R6, R12, R13; OI-40, OI-41). Validate and publish run every release rule and refuse
/// with the findings; publish stamps a content hash of the manifest and activates nothing. The active release cannot be retired,
/// because the engine would have no release to read. Activation is how dataset versions reach queries: it re-checks every enabled
/// report against the release, moves the tenant's pointer, advances the semantic revision the engine caches by, and logs who did
/// it, answering the tenant state. Only a draft is deleted.
/// </summary>
public sealed class ReleaseLifecycleTests
{
    /// <summary>A published release that pins only loans, which the report's GL source cannot read.</summary>
    private const long LoansOnlyReleaseId = 903;

    /// <summary>
    /// Validating the empty draft is refused with <c>manifest_not_empty</c> and, since the balance sheet's GL source would lose its
    /// dataset, <c>report_sources_resolve</c>; the status stays draft.
    /// </summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task Validate_RefusesBrokenRules()
    {
        var fixture = await CreateAsync();

        var refusal = (await fixture.Commands().ValidateAsync(DraftReleaseId, TestContext.Current.CancellationToken)).Refusal!;

        Assert.Equal(RefusalKind.RulesViolated, refusal.Kind);
        Assert.Equal(["manifest_not_empty", "report_sources_resolve"], refusal.Violations.Select(violation => violation.Code));
        Assert.Equal(PublicationStatuses.Draft, StatusOf(fixture, DraftReleaseId));
    }

    /// <summary>
    /// A validated release publishes with the hash of its manifest, stamped now, and the active release and revision stay: publishing
    /// activates nothing (R3). Publishing a draft is refused, because publish needs a validated release.
    /// </summary>
    /// <returns>A task that completes when the transitions are checked.</returns>
    [Fact]
    public async Task Publish_StampsTheContentHashAndActivatesNothing()
    {
        var fixture = await CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var row = new ReleaseDatasetRecord { SemanticReleaseId = DraftReleaseId, DatasetId = GlDatasetId, DatasetVersionId = GlVersionId };
        await fixture.Graph.Manifest.InsertAsync(row, cancellationToken);
        var commands = fixture.Commands();

        Assert.Equal(RefusalKind.Conflict, (await commands.PublishAsync(DraftReleaseId, cancellationToken)).Refusal!.Kind);
        Assert.True((await commands.ValidateAsync(DraftReleaseId, cancellationToken)).Succeeded);
        var published = (await commands.PublishAsync(DraftReleaseId, cancellationToken)).Value!;

        Assert.Equal((PublicationStatuses.Published, Now.UtcDateTime), (published.Release.Status, published.Release.PublishedAt!.Value));
        Assert.Equal(ReleaseContentHash.Of([row]), published.Release.ContentHash);
        Assert.Empty(published.Report.Violations);
        Assert.Equal((SemanticReleaseId, StartingRevision), (fixture.Graph.DatabaseInfo.All.Single().ActiveSemanticReleaseId!.Value, fixture.CurrentRevision));
    }

    /// <summary>The content hash depends only on what the manifest pins, not on the order of its rows.</summary>
    [Fact]
    public void ContentHash_IgnoresRowOrder()
    {
        ReleaseDatasetRecord[] rows = [Pin(GlDatasetId, GlVersionId), Pin(LoansDatasetId, LoansPublishedVersionId)];

        Assert.Equal(ReleaseContentHash.Of(rows), ReleaseContentHash.Of(rows.Reverse()));
        Assert.NotEqual(ReleaseContentHash.Of(rows), ReleaseContentHash.Of(rows[..1]));
    }

    /// <summary>The active release is refused with <c>release_not_active</c>; another published release retires.</summary>
    /// <returns>A task that completes when the transitions are checked.</returns>
    [Fact]
    public async Task Retire_RefusesTheActiveRelease()
    {
        var fixture = await CreateAsync();
        var commands = fixture.Commands();

        var refusal = (await commands.RetireAsync(SemanticReleaseId, TestContext.Current.CancellationToken)).Refusal!;
        var retired = (await commands.RetireAsync(PreviousReleaseId, TestContext.Current.CancellationToken)).Value!;

        Assert.Equal((RefusalKind.RulesViolated, "release_not_active"), (refusal.Kind, Assert.Single(refusal.Violations).Code));
        Assert.Equal(PublicationStatuses.Published, StatusOf(fixture, SemanticReleaseId));
        Assert.Equal(PublicationStatuses.Retired, retired.Status);
    }

    /// <summary>
    /// Activating an older release (a rollback) points the tenant at it, advances the revision and logs the activation with the caller
    /// and the release it replaced; the answer is the tenant state, and the release's detail and list item show the activation.
    /// </summary>
    /// <returns>A task that completes when the activation is checked.</returns>
    [Fact]
    public async Task Activate_MovesThePointerAdvancesTheRevisionAndLogs()
    {
        var fixture = await CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var tenant = (await fixture.Commands().ActivateAsync(PreviousReleaseId, "analyst", cancellationToken)).Value!;

        Assert.Equal((PreviousReleaseId, StartingRevision + 1), (tenant.ActiveReleaseId!.Value, tenant.SemanticRevision));
        var activation = Assert.Single((await fixture.Queries().GetAsync(PreviousReleaseId, cancellationToken)).Activations);
        Assert.Equal((PreviousReleaseId, (long?)SemanticReleaseId, Now.UtcDateTime, "analyst"), (activation.ReleaseId, activation.PreviousReleaseId, activation.ActivatedAt, activation.ActivatedBy));
        var item = (await fixture.Queries().ListAsync(cancellationToken)).Single(release => release.IsActive);
        Assert.Equal((PreviousReleaseId, Now.UtcDateTime, "analyst", null), (item.Id, item.ActivatedAt!.Value, item.ActivatedBy, item.DiffVsActive));
    }

    /// <summary>A draft is refused before anything is written.</summary>
    /// <returns>A task that completes when the activation is checked.</returns>
    [Fact]
    public async Task Activate_RefusesADraft()
    {
        var fixture = await CreateAsync();

        var refusal = (await fixture.Commands().ActivateAsync(DraftReleaseId, "analyst", TestContext.Current.CancellationToken)).Refusal!;

        Assert.Equal(RefusalKind.Conflict, refusal.Kind);
        Assert.Equal((SemanticReleaseId, StartingRevision), (fixture.Graph.DatabaseInfo.All.Single().ActiveSemanticReleaseId!.Value, fixture.CurrentRevision));
        Assert.Empty(fixture.Activations.All);
    }

    /// <summary>
    /// A release that would break an enabled report is refused (R12): the report's GL source reads a dataset the release does not pin.
    /// The release's check reports the same finding against the report.
    /// </summary>
    /// <returns>A task that completes when the activation is checked.</returns>
    [Fact]
    public async Task Activate_RefusesAReleaseThatBreaksAnEnabledReport()
    {
        var fixture = await CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.Releases.InsertAsync(Release(LoansOnlyReleaseId, "loans-only", PublicationStatuses.Published), cancellationToken);
        await fixture.Graph.Manifest.InsertAsync(
            new ReleaseDatasetRecord { SemanticReleaseId = LoansOnlyReleaseId, DatasetId = LoansDatasetId, DatasetVersionId = LoansPublishedVersionId }, cancellationToken);

        var refusal = (await fixture.Commands().ActivateAsync(LoansOnlyReleaseId, "analyst", cancellationToken)).Refusal!;
        var check = await fixture.Queries().CheckAsync(LoansOnlyReleaseId, cancellationToken);

        Assert.Equal((RefusalKind.RulesViolated, "report_sources_resolve"), (refusal.Kind, Assert.Single(refusal.Violations).Code));
        Assert.Equal(SemanticReleaseId, fixture.Graph.DatabaseInfo.All.Single().ActiveSemanticReleaseId!.Value);
        var finding = Assert.Single(check.Violations);
        Assert.Equal(("report_sources_resolve", (long?)null, "report", "60"), (finding.Code, finding.DatasetId, finding.Target!.Area, finding.Target.ItemId));
    }

    /// <summary>A new release copies another's manifest; its key must be free; and only a draft is deleted, with its manifest.</summary>
    /// <returns>A task that completes when the commands are checked.</returns>
    [Fact]
    public async Task CreateCopiesAndDeleteNeedsADraft()
    {
        var fixture = await CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var commands = fixture.Commands();

        var created = (await commands.CreateAsync(new CreateReleaseInput { Key = "prod/2027-q1", CopyFromReleaseId = SemanticReleaseId }, cancellationToken)).Value!;
        var taken = (await commands.CreateAsync(new CreateReleaseInput { Key = "prod/2027-q1", CopyFromReleaseId = null }, cancellationToken)).Refusal!;
        var published = await commands.DeleteAsync(SemanticReleaseId, cancellationToken);

        Assert.Equal(PublicationStatuses.Draft, created.Status);
        Assert.Equal(GlVersionId, Assert.Single(fixture.Graph.Manifest.All, row => row.SemanticReleaseId == created.Id).DatasetVersionId);
        Assert.Equal(RefusalKind.Conflict, taken.Kind);
        Assert.Equal(RefusalKind.Conflict, published!.Kind);
        Assert.Null(await commands.DeleteAsync(created.Id, cancellationToken));
        Assert.DoesNotContain(fixture.Graph.Manifest.All, row => row.SemanticReleaseId == created.Id);
        Assert.DoesNotContain(fixture.Releases.All, release => release.SemanticReleaseId == created.Id);
    }

    /// <summary>A release's status as stored.</summary>
    private static string StatusOf(ReleaseFixture fixture, long releaseId)
    {
        return fixture.Releases.All.Single(release => release.SemanticReleaseId == releaseId).Status;
    }
}
