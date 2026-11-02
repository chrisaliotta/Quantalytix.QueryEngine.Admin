using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Datasets.DatasetInputs;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms the dataset version change set the dataset editor saves through (blueprint 11, OI-01): edits apply to the live version in
/// memory, a preview names what it creates by the client's temporary ids, a commit writes the rows, maps the ids and advances the
/// semantic revision, a frozen version takes no edit, keys are fixed and unique (D4), deletes are refused while an item is read by id
/// (OI-35), managed tables take new columns and renames before the rows are written (OI-33), and commit findings track pending edits
/// and refuse the commit.
/// </summary>
public sealed class DatasetChangeSetTests
{
    /// <summary>The live version as operations carry its ids.</summary>
    private const long Live = DatasetFixture.LiveVersionId;

    /// <summary>The tables and services.</summary>
    private readonly DatasetFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A preview of a field and a grain over it, both new, shows them by their temporary ids and writes nothing.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Preview_PresentsCreatedItemsByTemporaryId()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(Live, Request(SaveColumnField("tmp:balance", "balance", "balance"), SaveGrain("tmp:row", "row", "tmp:balance")), Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal("tmp:balance", Assert.Single(outcome.Value.State.Fields).Id);
        Assert.Equal(["tmp:balance"], Assert.Single(outcome.Value.State.Grains).FieldIds);
        Assert.Empty(_fixture.Fields.All);
    }

    /// <summary>
    /// A commit writes the field and the grain component with real ids, answers the temporary-to-real map, and advances the semantic
    /// revision once: the engine reads the live version at once.
    /// </summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_WritesRowsAndMapsTemporaryIds()
    {
        var outcome = await _fixture.Pipeline().CommitAsync(Live, Request(SaveColumnField("tmp:balance", "balance", "balance"), SaveGrain("tmp:row", "row", "tmp:balance")), Token);

        Assert.True(outcome.Succeeded);
        var fieldId = outcome.Value.IdMap["tmp:balance"];
        Assert.Equal(fieldId, Assert.Single(_fixture.GrainFields.All).FieldId);
        Assert.Equal(fieldId.ToString(CultureInfo.InvariantCulture), Assert.Single(outcome.Value.State.Fields).Id);
        Assert.Equal(DatasetFixture.StartingRevision + 1, _fixture.Revision);
    }

    /// <summary>
    /// A frozen snapshot is read-only: a content edit and an identity edit of its version are both conflicts, and nothing is written.
    /// </summary>
    /// <returns>A task that completes when the commits are checked.</returns>
    [Fact]
    public async Task FrozenVersion_TakesNoEdit()
    {
        var content = await _fixture.Pipeline().CommitAsync(DatasetFixture.FrozenVersionId, Request(SaveColumnField(null, "balance", "balance")), Token);
        var identity = await _fixture.Pipeline().CommitAsync(
            DatasetFixture.FrozenVersionId,
            Request(new DatasetUpdateDatasetOperation { Patch = new DatasetPatch { DisplayName = "Loans daily" } }),
            Token);

        Assert.Equal(RefusalKind.Conflict, content.Refusal!.Kind);
        Assert.Equal(RefusalKind.Conflict, identity.Refusal!.Kind);
        Assert.Equal("Loans", _fixture.Datasets.All.Single().DisplayName);
        Assert.Equal(DatasetFixture.StartingRevision, _fixture.Revision);
    }

    /// <summary>The change-set state carries the dataset identity, so a preview shows a staged identity edit before it is written.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Preview_ShowsTheStagedIdentity()
    {
        var preview = await _fixture.Pipeline().PreviewAsync(
            Live, Request(new DatasetUpdateDatasetOperation { Patch = new DatasetPatch { DisplayName = "Loans daily" } }), Token);

        Assert.Equal("Loans daily", preview.Value!.State.Dataset.DisplayName);
        Assert.NotEqual("Loans daily", _fixture.Datasets.All.Single().DisplayName);
    }

    /// <summary>A second field with a taken key is refused as input on the operation's <c>input.key</c>, and nothing is written.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task TakenFieldKey_IsRefusedAsInput()
    {
        var outcome = await _fixture.Pipeline().CommitAsync(Live, Request(SaveColumnField(null, "balance", "balance"), SaveColumnField(null, "balance", "loan_id")), Token);

        Assert.Equal(RefusalKind.Invalid, outcome.Refusal!.Kind);
        Assert.Contains("operations[1].input.key", outcome.Refusal.Errors.Keys);
        Assert.Empty(_fixture.Fields.All);
    }

    /// <summary>A stored field keeps its key (D4): saving it under another key is refused.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task StoredFieldKey_IsFixed()
    {
        await _fixture.Fields.InsertAsync(DatasetRows.ColumnField(5, Live, "balance", "balance"), Token);

        var outcome = await _fixture.Pipeline().PreviewAsync(Live, Request(SaveColumnField("5", "endingBalance", "balance")), Token);

        Assert.Contains("operations[0].input.key", outcome.Refusal!.Errors.Keys);
    }

    /// <summary>A field a grain names by id is not deleted: the refusal is a blocked delete coded <c>field_in_use</c>.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DeletingAFieldAGrainReads_IsBlocked()
    {
        await _fixture.Fields.InsertAsync(DatasetRows.ColumnField(5, Live, "balance", "balance"), Token);
        await _fixture.Grains.InsertAsync(DatasetRows.Grain(6, Live, "row", GrainKinds.Row), Token);
        await _fixture.GrainFields.InsertAsync(DatasetRows.GrainField(6, 1, 5), Token);

        var outcome = await _fixture.Pipeline().PreviewAsync(Live, Request(new DatasetDeleteFieldOperation { FieldId = "5" }), Token);

        Assert.Equal(RefusalKind.DeleteBlocked, outcome.Refusal!.Kind);
        Assert.Equal(DatasetItemBlockerCodes.FieldInUse, Assert.Single(outcome.Refusal.Violations).Code);
    }

    /// <summary>The version read model lists what reads each item (OI-35): the grain reads the field, which blocks its delete.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Dependents_NameWhatReadsEachItem()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(Live, Request(SaveColumnField("tmp:balance", "balance", "balance"), SaveGrain("tmp:row", "row", "tmp:balance")), Token);

        var dependent = Assert.Single(outcome.Value!.State.Dependents["tmp:balance"]);
        Assert.Equal(DatasetTabs.Grains, dependent.Tab);
        Assert.Equal("tmp:row", dependent.ItemId);
        Assert.True(dependent.BlocksDelete);
    }

    /// <summary>
    /// Fields made from columns carry temporary ids that follow from the operation, so every replay of the same operations names the
    /// same fields; a column fields cannot read is refused on its index.
    /// </summary>
    /// <returns>A task that completes when the previews are checked.</returns>
    [Fact]
    public async Task FieldsFromColumns_KeepTheirIdsAcrossReplays()
    {
        var request = Request(new DatasetAddFieldsFromColumnsOperation { Columns = ["loan_id", "branch_code"] });

        var first = await _fixture.Pipeline().PreviewAsync(Live, request, Token);
        var second = await _fixture.Pipeline().PreviewAsync(Live, request, Token);
        var unreadable = await _fixture.Pipeline().PreviewAsync(Live, Request(new DatasetAddFieldsFromColumnsOperation { Columns = ["sketch"] }), Token);

        Assert.Equal(["branchCode", "loanId"], first.Value!.State.Fields.Select(field => field.Key));
        Assert.Equal(first.Value.State.Fields.Select(field => field.Id), second.Value!.State.Fields.Select(field => field.Id));
        Assert.All(first.Value.State.Fields, field => Assert.StartsWith(TemporaryIds.Prefix, field.Id, StringComparison.Ordinal));
        Assert.Contains("operations[0].columns[0]", unreadable.Refusal!.Errors.Keys);
    }

    /// <summary>A field that creates its column is refused on a table the API did not create: DDL runs on managed tables only (OI-33).</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task CreateColumn_OnAnUnmanagedTable_IsAConflict()
    {
        var operation = SaveColumnField(null, "riskBand", "risk_band");

        var outcome = await _fixture.Pipeline().PreviewAsync(Live, Request(operation with { Input = operation.Input with { CreateColumn = true } }), Token);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
        Assert.Empty(_fixture.ManagedTables.Calls);
    }

    /// <summary>
    /// On a managed table, the commit adds the new column before it writes the field, and the preview already reads the column, so
    /// <c>field_columns_exist</c> does not report it.
    /// </summary>
    /// <returns>A task that completes when the preview and commit are checked.</returns>
    [Fact]
    public async Task CreateColumn_OnAManagedTable_AddsTheColumnBeforeTheRows()
    {
        await _fixture.UseManagedTableAsync();
        var operation = SaveColumnField(null, "riskBand", "risk_band");
        var request = Request(operation with { Input = operation.Input with { CreateColumn = true } });

        var preview = await _fixture.Changes().PreviewAsync(Live, request, Token);
        var commit = await _fixture.Changes().CommitAsync(Live, request, Token);

        Assert.DoesNotContain(preview.Value!.Violations, violation => violation.Code == "field_columns_exist");
        Assert.True(commit.Succeeded);
        Assert.Equal(["add staging_loans risk_band"], _fixture.ManagedTables.Calls);
        Assert.Equal("risk_band", Assert.Single(_fixture.Fields.All).SourceColumn);
    }

    /// <summary>
    /// A managed table that only the live version reads is renamed before the rows; the version and the registry then name the new
    /// table. Another version reading it, live or frozen, keeps its name, so the rename is a conflict (OI-33).
    /// </summary>
    /// <returns>A task that completes when the commits are checked.</returns>
    [Fact]
    public async Task RenameSourceTable_RenamesAManagedTableOnlyTheLiveVersionReads()
    {
        await _fixture.UseManagedTableAsync();
        var rename = Request(new DatasetRenameSourceTableOperation { TableName = "loan_staging" });

        var renamed = await _fixture.Changes().CommitAsync(Live, rename, Token);
        await _fixture.Versions.InsertAsync(DatasetRows.Version(101) with { SnapshotId = 3, SourceObject = "loan_staging" }, Token);
        var refused = await _fixture.Changes().CommitAsync(Live, Request(new DatasetRenameSourceTableOperation { TableName = "loan_staging_2" }), Token);

        Assert.True(renamed.Succeeded);
        Assert.Equal(["rename staging_loans loan_staging"], _fixture.ManagedTables.Calls);
        Assert.Equal("loan_staging", _fixture.Versions.All.Single(version => version.DatasetVersionId == Live).SourceObject);
        Assert.Equal("loan_staging", _fixture.Managed.All.Single().SourceObject);
        Assert.Equal(RefusalKind.Conflict, refused.Refusal!.Kind);
    }

    /// <summary>
    /// One commit that renames the managed table and adds a column runs both statements in one write plan, the rename first and the
    /// column on the new name, before the rows.
    /// </summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_RunsTheRenameThenTheNewColumns()
    {
        await _fixture.UseManagedTableAsync();
        var save = SaveColumnField(null, "riskBand", "risk_band");

        var commit = await _fixture.Changes().CommitAsync(
            Live, Request(save with { Input = save.Input with { CreateColumn = true } }, new DatasetRenameSourceTableOperation { TableName = "loan_staging" }), Token);

        Assert.True(commit.Succeeded);
        Assert.Equal(["rename staging_loans loan_staging", "add loan_staging risk_band"], _fixture.ManagedTables.Calls);
    }

    /// <summary>
    /// DDL cannot be rolled back: when a commit stopped after its DDL, replaying the same operations finds the table renamed and the
    /// column added, plans no DDL, and writes the rows.
    /// </summary>
    /// <returns>A task that completes when the replay is checked.</returns>
    [Fact]
    public async Task Replay_AfterTheDdlRan_PlansNoDdl()
    {
        await _fixture.UseManagedTableAsync();
        await _fixture.ManagedTables.RenameAsync("staging_loans", "loan_staging", Token);
        await _fixture.ManagedTables.AddColumnsAsync(
            "loan_staging", [new DatasetNewColumnInput { Name = "risk_band", DataType = DataTypes.String, NumericPrecision = null, NumericScale = null, IsNullable = true }], Token);
        var save = SaveColumnField(null, "riskBand", "risk_band");

        var commit = await _fixture.Changes().CommitAsync(
            Live, Request(new DatasetRenameSourceTableOperation { TableName = "loan_staging" }, save with { Input = save.Input with { CreateColumn = true } }), Token);

        Assert.True(commit.Succeeded);
        Assert.Equal(["rename staging_loans loan_staging", "add loan_staging risk_band"], _fixture.ManagedTables.Calls);
        Assert.Equal("loan_staging", _fixture.Managed.All.Single().SourceObject);
    }

    /// <summary>Commit findings come back in the preview and reflect the pending edits, so the editor flags them before Save.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task CommitFindings_TrackPendingEdits()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(Live, Request(SaveExpressionField(null, "share", "[f.missing] / 2")), Token);

        var violation = Assert.Single(outcome.Value!.Violations);
        Assert.Equal("expression_field_references", violation.Code);
        Assert.Equal(DatasetTabs.Fields, violation.Target!.Area);
    }

    /// <summary>
    /// The live version is what the engine reads, so a rule that only gated publishing before now refuses the commit: an expression
    /// field naming a missing field is refused with 422, nothing is written and the semantic revision stays.
    /// </summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task Commit_ThatBreaksAFormerPublicationRule_IsRefused()
    {
        var outcome = await _fixture.Pipeline().CommitAsync(Live, Request(SaveExpressionField(null, "share", "[f.missing] / 2")), Token);

        Assert.Equal(RefusalKind.RulesViolated, outcome.Refusal!.Kind);
        Assert.Equal("expression_field_references", Assert.Single(outcome.Refusal.Violations).Code);
        Assert.Empty(_fixture.Fields.All);
        Assert.Equal(DatasetFixture.StartingRevision, _fixture.Revision);
    }

    /// <summary>A JSON path field saved with <c>materialize</c> gets its column in the source object's view once the commit has written it.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task MaterializeOnSave_AddsTheColumnToTheView()
    {
        var outcome = await _fixture.Changes().CommitAsync(Live, Request(SaveJsonField(null, "rating", "$.risk.rating", materialize: true)), Token);

        var field = Assert.Single(outcome.Value!.State.Fields);
        Assert.Equal(MaterializationStates.Building, field.Materialization!.State);
        var view = _fixture.Views.Definitions[MaterializedViewNames.ViewOf(DatasetFixture.LoanTable)];
        Assert.Equal(field.Materialization.ColumnName, Assert.Single(view.Columns).Name);
    }
}
