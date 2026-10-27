using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms the dataset manager's lifecycle commands (blueprint 11): creating a dataset over an existing object or a new managed
/// table (OI-33, OI-36), one open version per dataset (OI-23) with clones recording their source and time (OI-12), deleting only
/// open, unpinned versions and datasets (D10, OI-17), and the strict transitions validate, publish, reopen and retire (D1).
/// </summary>
public sealed class DatasetCommandsTests
{
    /// <summary>The tables and services.</summary>
    private readonly DatasetFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A dataset over an existing table gets draft version 1 with a field for every column fields can read.</summary>
    /// <returns>A task that completes when the dataset is checked.</returns>
    [Fact]
    public async Task CreateFromExisting_GivesDraftOneAFieldPerReadableColumn()
    {
        var outcome = await _fixture.Commands().CreateAsync(Existing("bank.loan_snapshots"), Token);

        var version = Assert.Single(outcome.Value!.Versions);
        Assert.Equal((1, PublicationStatuses.Draft), (version.VersionNumber, version.Status));
        Assert.Equal(_fixture.Time.GetUtcNow().UtcDateTime, version.CreatedAt);
        Assert.Equal(["attributes", "balance", "branch_code", "loan_id"], _fixture.Fields.All.Select(field => field.SourceColumn).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A dataset over a new table creates and registers the table before any metadata row, so a failure leaves only a registered
    /// table; each requested column becomes a field.
    /// </summary>
    /// <returns>A task that completes when the dataset is checked.</returns>
    [Fact]
    public async Task CreateWithNewTable_CreatesAndRegistersTheTableFirst()
    {
        var input = new DatasetCreateWithNewTableInput
        {
            Key = "bank.budget",
            DisplayName = "Budget",
            TableName = "budget_lines",
            Columns = [new DatasetNewColumnInput { Name = "amount", DataType = DataTypes.Decimal, NumericPrecision = 18, NumericScale = 2, IsNullable = false }]
        };

        var outcome = await _fixture.Commands().CreateAsync(input, Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal(["create budget_lines"], _fixture.ManagedTables.Calls);
        Assert.Contains(_fixture.Managed.All, row => row.SourceObject == "budget_lines" && row.CreatedByDatasetId == long.Parse(outcome.Value.Id, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("amount", Assert.Single(_fixture.Fields.All).SourceColumn);
    }

    /// <summary>Dataset keys are unique ignoring case; a taken key is a conflict and nothing is written.</summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task TakenKey_IsAConflict()
    {
        var outcome = await _fixture.Commands().CreateAsync(Existing("Bank.Loans"), Token);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
        Assert.Single(_fixture.Datasets.All);
    }

    /// <summary>A dataset has at most one open version (OI-23): a draft while version 2 is a draft is a conflict.</summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task CreateDraft_WhileAnOpenVersionExists_IsAConflict()
    {
        var outcome = await _fixture.Commands().CreateDraftAsync(DatasetFixture.DatasetId, new DatasetCreateEmptyDraftInput { SourceObjectKind = SourceObjectKinds.Table, SourceObject = DatasetFixture.LoanTable }, Token);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
    }

    /// <summary>
    /// A clone of the published version, once the draft is gone, takes the number after the highest remaining version and records its
    /// source version and its creation time.
    /// </summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task CloneDraft_RecordsItsSourceAndCreationTime()
    {
        var commands = _fixture.Commands();
        Assert.Null(await commands.DeleteVersionAsync(DatasetFixture.DraftVersionId, Token));

        var outcome = await commands.CreateDraftAsync(DatasetFixture.DatasetId, new DatasetCloneDraftInput { FromVersionId = DatasetFixture.PublishedVersionId }, Token);

        Assert.Equal(2, outcome.Value!.VersionNumber);
        Assert.Equal("99", outcome.Value.ClonedFromVersionId);
        Assert.Equal(_fixture.Time.GetUtcNow().UtcDateTime, outcome.Value.CreatedAt);
    }

    /// <summary>A published version is never deleted, and neither is an open version a release pins; both are coded blockers.</summary>
    /// <returns>A task that completes when the refusals are checked.</returns>
    [Fact]
    public async Task DeleteVersion_IsBlockedWhenPublishedOrPinned()
    {
        await _fixture.PinAsync(DatasetFixture.DraftVersionId, active: false);

        var published = await _fixture.Commands().DeleteVersionAsync(DatasetFixture.PublishedVersionId, Token);
        var pinned = await _fixture.Commands().DeleteVersionAsync(DatasetFixture.DraftVersionId, Token);

        Assert.Equal(DeleteBlockerCodes.PublishedVersion, Assert.Single(published!.Violations).Code);
        Assert.Equal(DeleteBlockerCodes.ReferencedByRelease, Assert.Single(pinned!.Violations).Code);
        Assert.Equal(2, _fixture.Versions.All.Count);
    }

    /// <summary>A dataset with a published version is not deleted, and its delete check says why.</summary>
    /// <returns>A task that completes when the check and refusal are checked.</returns>
    [Fact]
    public async Task DeleteDataset_IsBlockedByPublishedVersions()
    {
        var check = await _fixture.Queries().DatasetDeleteCheckAsync(DatasetFixture.DatasetId, Token);
        var refusal = await _fixture.Commands().DeleteAsync(DatasetFixture.DatasetId, Token);

        Assert.False(check.Allowed);
        Assert.Equal(DeleteBlockerCodes.HasPublishedVersions, Assert.Single(check.Reasons).Code);
        Assert.Equal(RefusalKind.DeleteBlocked, refusal!.Kind);
    }

    /// <summary>Publishing a draft validates it first in the same call, stamping the publication time.</summary>
    /// <returns>A task that completes when the version is checked.</returns>
    [Fact]
    public async Task Publish_FromDraft_ValidatesThenPublishes()
    {
        var outcome = await _fixture.Commands().PublishAsync(DatasetFixture.DraftVersionId, Token);

        Assert.Equal(PublicationStatuses.Published, outcome.Value!.Status);
        Assert.Equal(_fixture.Time.GetUtcNow().UtcDateTime, outcome.Value.PublishedAt);
    }

    /// <summary>A draft that breaks a rule is not validated: the refusal carries the violations and the status stays draft.</summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task Validate_WhenARuleFails_KeepsTheDraft()
    {
        await _fixture.Fields.InsertAsync(DatasetRows.ColumnField(5, DatasetFixture.DraftVersionId, "gone", "no_such_column"), Token);

        var outcome = await _fixture.Commands().ValidateAsync(DatasetFixture.DraftVersionId, Token);

        Assert.Equal(RefusalKind.RulesViolated, outcome.Refusal!.Kind);
        Assert.Contains(outcome.Refusal.Violations, violation => violation.Code == "field_columns_exist");
        Assert.Equal(PublicationStatuses.Draft, _fixture.Versions.All.Single(version => version.DatasetVersionId == DatasetFixture.DraftVersionId).Status);
    }

    /// <summary>Each strict transition starts from its own status: reopening a draft or retiring a draft is a conflict.</summary>
    /// <returns>A task that completes when the refusals are checked.</returns>
    [Fact]
    public async Task Transitions_FromTheWrongStatus_AreConflicts()
    {
        var commands = _fixture.Commands();

        Assert.Equal(RefusalKind.Conflict, (await commands.ReopenAsync(DatasetFixture.DraftVersionId, Token)).Refusal!.Kind);
        Assert.Equal(RefusalKind.Conflict, (await commands.RetireAsync(DatasetFixture.DraftVersionId, Token)).Refusal!.Kind);
        Assert.Equal(RefusalKind.Conflict, (await commands.ValidateAsync(DatasetFixture.PublishedVersionId, Token)).Refusal!.Kind);
    }

    /// <summary>A validated version returns to draft, and a published one retires.</summary>
    /// <returns>A task that completes when the statuses are checked.</returns>
    [Fact]
    public async Task ReopenAndRetire_MoveTheirVersions()
    {
        var commands = _fixture.Commands();
        await commands.ValidateAsync(DatasetFixture.DraftVersionId, Token);

        var reopened = await commands.ReopenAsync(DatasetFixture.DraftVersionId, Token);
        var retired = await commands.RetireAsync(DatasetFixture.PublishedVersionId, Token);

        Assert.Equal(PublicationStatuses.Draft, reopened.Value!.Status);
        Assert.Equal(PublicationStatuses.Retired, retired.Value!.Status);
    }

    /// <summary>A dataset over <c>loan_daily</c>.</summary>
    private static DatasetCreateFromExistingInput Existing(string key)
    {
        return new DatasetCreateFromExistingInput { Key = key, DisplayName = key, SourceObjectKind = SourceObjectKinds.Table, SourceObject = DatasetFixture.LoanTable };
    }
}
