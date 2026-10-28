using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Datasets.DatasetInputs;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms the dataset version change set the dataset editor saves through (blueprint 11, OI-01): edits apply to a draft in memory,
/// a preview names what it creates by the client's temporary ids, a commit writes the rows and maps the ids, a version that is not a
/// draft takes identity edits only (D3), keys are fixed and unique (D4), deletes are refused while an item is read by id (OI-35),
/// managed tables take new columns and renames before the rows are written (OI-33), and publication findings track pending edits.
/// </summary>
public sealed class DatasetChangeSetTests
{
    /// <summary>The draft as operations carry its ids.</summary>
    private const long Draft = DatasetFixture.DraftVersionId;

    /// <summary>The tables and services.</summary>
    private readonly DatasetFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A preview of a field and a grain over it, both new, shows them by their temporary ids and writes nothing.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Preview_PresentsCreatedItemsByTemporaryId()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(Draft, Request(SaveColumnField("tmp:balance", "balance", "balance"), SaveGrain("tmp:row", "row", "tmp:balance")), Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal("tmp:balance", Assert.Single(outcome.Value.State.Fields).Id);
        Assert.Equal(["tmp:balance"], Assert.Single(outcome.Value.State.Grains).FieldIds);
        Assert.Empty(_fixture.Fields.All);
    }

    /// <summary>
    /// A commit writes the field and the grain component with real ids, answers the temporary-to-real map, and leaves the semantic
    /// revision alone: no query reads a draft.
    /// </summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_WritesRowsAndMapsTemporaryIds()
    {
        var outcome = await _fixture.Pipeline().CommitAsync(Draft, Request(SaveColumnField("tmp:balance", "balance", "balance"), SaveGrain("tmp:row", "row", "tmp:balance")), Token);

        Assert.True(outcome.Succeeded);
        var fieldId = outcome.Value.IdMap["tmp:balance"];
        Assert.Equal(fieldId, Assert.Single(_fixture.GrainFields.All).FieldId);
        Assert.Equal(fieldId.ToString(CultureInfo.InvariantCulture), Assert.Single(outcome.Value.State.Fields).Id);
        Assert.Equal(DatasetFixture.StartingRevision, _fixture.Revision);
    }

    /// <summary>
    /// A published version never changes (D1): a content edit is a conflict, while an identity edit applies and advances the
    /// semantic revision, because the engine reads the dataset row.
    /// </summary>
    /// <returns>A task that completes when the commits are checked.</returns>
    [Fact]
    public async Task PublishedVersion_TakesIdentityEditsOnly()
    {
        var content = await _fixture.Pipeline().CommitAsync(DatasetFixture.PublishedVersionId, Request(SaveColumnField(null, "balance", "balance")), Token);
        var identity = await _fixture.Pipeline().CommitAsync(
            DatasetFixture.PublishedVersionId,
            Request(new DatasetUpdateDatasetOperation { Patch = new DatasetPatch { DisplayName = "Loans daily" } }),
            Token);

        Assert.Equal(RefusalKind.Conflict, content.Refusal!.Kind);
        Assert.True(identity.Succeeded);
        Assert.Equal("Loans daily", _fixture.Datasets.All.Single().DisplayName);
        Assert.Equal(DatasetFixture.StartingRevision + 1, _fixture.Revision);
    }

    /// <summary>A second field with a taken key is refused as input on the operation's <c>input.key</c>, and nothing is written.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task TakenFieldKey_IsRefusedAsInput()
    {
        var outcome = await _fixture.Pipeline().CommitAsync(Draft, Request(SaveColumnField(null, "balance", "balance"), SaveColumnField(null, "balance", "loan_id")), Token);

        Assert.Equal(RefusalKind.Invalid, outcome.Refusal!.Kind);
        Assert.Contains("operations[1].input.key", outcome.Refusal.Errors.Keys);
        Assert.Empty(_fixture.Fields.All);
    }

    /// <summary>A stored field keeps its key (D4): saving it under another key is refused.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task StoredFieldKey_IsFixed()
    {
        await _fixture.Fields.InsertAsync(DatasetRows.ColumnField(5, Draft, "balance", "balance"), Token);

        var outcome = await _fixture.Pipeline().PreviewAsync(Draft, Request(SaveColumnField("5", "endingBalance", "balance")), Token);

        Assert.Contains("operations[0].input.key", outcome.Refusal!.Errors.Keys);
    }

    /// <summary>A field a grain names by id is not deleted: the refusal is a blocked delete coded <c>field_in_use</c>.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DeletingAFieldAGrainReads_IsBlocked()
    {
        await _fixture.Fields.InsertAsync(DatasetRows.ColumnField(5, Draft, "balance", "balance"), Token);
        await _fixture.Grains.InsertAsync(DatasetRows.Grain(6, Draft, "row", GrainKinds.Row), Token);
        await _fixture.GrainFields.InsertAsync(DatasetRows.GrainField(6, 1, 5), Token);

        var outcome = await _fixture.Pipeline().PreviewAsync(Draft, Request(new DatasetDeleteFieldOperation { FieldId = "5" }), Token);

        Assert.Equal(RefusalKind.DeleteBlocked, outcome.Refusal!.Kind);
        Assert.Equal(DatasetItemBlockerCodes.FieldInUse, Assert.Single(outcome.Refusal.Violations).Code);
    }

    /// <summary>The version read model lists what reads each item (OI-35): the grain reads the field, which blocks its delete.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Dependents_NameWhatReadsEachItem()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(Draft, Request(SaveColumnField("tmp:balance", "balance", "balance"), SaveGrain("tmp:row", "row", "tmp:balance")), Token);

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

        var first = await _fixture.Pipeline().PreviewAsync(Draft, request, Token);
        var second = await _fixture.Pipeline().PreviewAsync(Draft, request, Token);
        var unreadable = await _fixture.Pipeline().PreviewAsync(Draft, Request(new DatasetAddFieldsFromColumnsOperation { Columns = ["sketch"] }), Token);

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

        var outcome = await _fixture.Pipeline().PreviewAsync(Draft, Request(operation with { Input = operation.Input with { CreateColumn = true } }), Token);

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

        var preview = await _fixture.Changes().PreviewAsync(Draft, request, Token);
        var commit = await _fixture.Changes().CommitAsync(Draft, request, Token);

        Assert.DoesNotContain(preview.Value!.Violations, violation => violation.Code == "field_columns_exist");
        Assert.True(commit.Succeeded);
        Assert.Equal(["add staging_loans risk_band"], _fixture.ManagedTables.Calls);
        Assert.Equal("risk_band", Assert.Single(_fixture.Fields.All).SourceColumn);
    }

    /// <summary>
    /// A managed table that only the draft reads is renamed before the rows; the version and the registry then name the new table.
    /// Another version reading it keeps its name, so the rename is a conflict (OI-33).
    /// </summary>
    /// <returns>A task that completes when the commits are checked.</returns>
    [Fact]
    public async Task RenameSourceTable_RenamesAManagedTableOnlyTheDraftReads()
    {
        await _fixture.UseManagedTableAsync();
        var rename = Request(new DatasetRenameSourceTableOperation { TableName = "loan_staging" });

        var renamed = await _fixture.Changes().CommitAsync(Draft, rename, Token);
        await _fixture.Versions.InsertAsync(DatasetRows.Version(101) with { VersionNumber = 3, SourceObject = "loan_staging" }, Token);
        var refused = await _fixture.Changes().CommitAsync(Draft, Request(new DatasetRenameSourceTableOperation { TableName = "loan_staging_2" }), Token);

        Assert.True(renamed.Succeeded);
        Assert.Equal(["rename staging_loans loan_staging"], _fixture.ManagedTables.Calls);
        Assert.Equal("loan_staging", _fixture.Versions.All.Single(version => version.DatasetVersionId == Draft).SourceObject);
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
            Draft, Request(save with { Input = save.Input with { CreateColumn = true } }, new DatasetRenameSourceTableOperation { TableName = "loan_staging" }), Token);

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
            Draft, Request(new DatasetRenameSourceTableOperation { TableName = "loan_staging" }, save with { Input = save.Input with { CreateColumn = true } }), Token);

        Assert.True(commit.Succeeded);
        Assert.Equal(["rename staging_loans loan_staging", "add loan_staging risk_band"], _fixture.ManagedTables.Calls);
        Assert.Equal("loan_staging", _fixture.Managed.All.Single().SourceObject);
    }

    /// <summary>Publication findings come back in the preview and reflect the pending edits, without blocking the save.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task PublicationFindings_TrackPendingEdits()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(Draft, Request(SaveExpressionField(null, "share", "[f.missing] / 2")), Token);

        var violation = Assert.Single(outcome.Value!.Violations);
        Assert.Equal("expression_field_references", violation.Code);
        Assert.Equal(DatasetTabs.Fields, violation.Target!.Area);
    }

    /// <summary>A JSON path field saved with <c>materialize</c> gets its column in the source object's view once the commit has written it.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task MaterializeOnSave_AddsTheColumnToTheView()
    {
        var outcome = await _fixture.Changes().CommitAsync(Draft, Request(SaveJsonField(null, "rating", "$.risk.rating", materialize: true)), Token);

        var field = Assert.Single(outcome.Value!.State.Fields);
        Assert.Equal(MaterializationStates.Building, field.Materialization!.State);
        var view = _fixture.Views.Definitions[MaterializedViewNames.ViewOf(DatasetFixture.LoanTable)];
        Assert.Equal(field.Materialization.ColumnName, Assert.Single(view.Columns).Name);
    }
}
