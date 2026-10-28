using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms the manifest change set (OI-01, OI-37): the release editor keeps the whole manifest as its pending state and stages one
/// <c>saveManifest</c>. A preview answers the release rules over the pending manifest and what activating it would change, and writes
/// nothing; a commit writes the manifest. Only a draft's manifest changes (R2), each dataset is pinned once, and a pin of another
/// dataset's version or of an unpublished one is refused, because the engine would silently drop that dataset.
/// </summary>
public sealed class ReleaseChangeSetTests
{
    /// <summary>The preview of an unsaved manifest reports on it, diffs it from the active release and writes nothing.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Preview_ChecksAndDiffsThePendingManifest()
    {
        var fixture = await CreateAsync();

        var preview = Assert.IsType<ReleaseChangeSetPreview>((await PreviewAsync(fixture, Entry(GlDatasetId, GlVersionId), Entry(LoansDatasetId, LoansPublishedVersionId))).Value);

        Assert.Equal([GlDatasetId, LoansDatasetId], preview.State.Manifest.Select(entry => entry.DatasetId));
        Assert.True(preview.Report.Unsaved);
        Assert.Empty(preview.Violations);
        var diff = preview.DiffVsActive!;
        Assert.Equal((SemanticReleaseId, DraftReleaseId, 1), (diff.FromReleaseId, diff.ToReleaseId, diff.Unchanged));
        Assert.Equal(LoansDatasetId, Assert.Single(diff.Added).DatasetId);
        Assert.DoesNotContain(fixture.Graph.Manifest.All, row => row.SemanticReleaseId == DraftReleaseId);
    }

    /// <summary>An empty manifest saves, but the preview reports <c>manifest_not_empty</c>, which blocks validate.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Preview_ReportsPublicationFindingsWithoutRefusing()
    {
        var fixture = await CreateAsync();

        var preview = (await PreviewAsync(fixture)).Value!;

        Assert.Equal("manifest_not_empty", Assert.Single(preview.Report.Violations).Code);
        Assert.Equal(preview.Report.Violations, preview.Violations);
    }

    /// <summary>A pin of loans' draft is refused with the gate's finding, and nothing is written.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_RefusesAnUnpublishedPin()
    {
        var fixture = await CreateAsync();

        var refusal = (await CommitAsync(fixture, Entry(LoansDatasetId, LoansDraftVersionId))).Refusal!;

        Assert.Equal(RefusalKind.RulesViolated, refusal.Kind);
        Assert.Equal("pinned_version_published", Assert.Single(refusal.Violations).Code);
        Assert.DoesNotContain(fixture.Graph.Manifest.All, row => row.SemanticReleaseId == DraftReleaseId);
    }

    /// <summary>A commit writes the manifest and answers it as stored; a draft's manifest does not reach queries, so the revision stays.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_WritesTheManifest()
    {
        var fixture = await CreateAsync();

        var commit = (await CommitAsync(fixture, Entry(LoansDatasetId, LoansPublishedVersionId))).Value!;

        Assert.Equal(LoansPublishedVersionId, Assert.Single(commit.State.Manifest).DatasetVersionId);
        Assert.Equal(LoansPublishedVersionId, Assert.Single(fixture.Graph.Manifest.All, row => row.SemanticReleaseId == DraftReleaseId).DatasetVersionId);
        Assert.Equal(StartingRevision, fixture.CurrentRevision);
    }

    /// <summary>A dataset pinned twice is an input error on the operation; a dataset that no longer exists is a conflict.</summary>
    /// <returns>A task that completes when the refusals are checked.</returns>
    [Fact]
    public async Task DuplicateAndMissingDatasets_AreRefused()
    {
        var fixture = await CreateAsync();

        var duplicate = (await PreviewAsync(fixture, Entry(GlDatasetId, GlVersionId), Entry(GlDatasetId, GlVersionId))).Refusal!;
        var missing = (await PreviewAsync(fixture, Entry(77, GlVersionId))).Refusal!;

        Assert.Equal(RefusalKind.Invalid, duplicate.Kind);
        Assert.Equal(["Pin each dataset once."], Assert.Single(duplicate.Errors).Value);
        Assert.Equal(RefusalKind.Conflict, missing.Kind);
    }

    /// <summary>The manifest of a published release does not change.</summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task PublishedRelease_IsRefused()
    {
        var fixture = await CreateAsync();

        var refusal = (await fixture.Changes().CommitAsync(SemanticReleaseId, Request(), TestContext.Current.CancellationToken)).Refusal!;

        Assert.Equal((RefusalKind.Conflict, "Release is not a draft"), (refusal.Kind, refusal.Title));
    }

    /// <summary>Previews one <c>saveManifest</c> on the draft.</summary>
    private static Task<Outcome<ReleaseChangeSetPreview>> PreviewAsync(ReleaseFixture fixture, params ManifestEntry[] manifest)
    {
        return fixture.Changes().PreviewAsync(DraftReleaseId, Request(manifest), TestContext.Current.CancellationToken);
    }

    /// <summary>Commits one <c>saveManifest</c> on the draft.</summary>
    private static Task<Outcome<ChangeSetCommit<ReleaseDetail>>> CommitAsync(ReleaseFixture fixture, params ManifestEntry[] manifest)
    {
        return fixture.Changes().CommitAsync(DraftReleaseId, Request(manifest), TestContext.Current.CancellationToken);
    }

    /// <summary>A change set of one <c>saveManifest</c>.</summary>
    private static ChangeSetRequest<ReleaseOperation> Request(params ManifestEntry[] manifest)
    {
        return new ChangeSetRequest<ReleaseOperation> { Operations = [new ReleaseSaveManifestOperation { Manifest = manifest }] };
    }

    /// <summary>A manifest entry.</summary>
    private static ManifestEntry Entry(long datasetId, long datasetVersionId)
    {
        return new ManifestEntry { DatasetId = datasetId, DatasetVersionId = datasetVersionId };
    }
}
