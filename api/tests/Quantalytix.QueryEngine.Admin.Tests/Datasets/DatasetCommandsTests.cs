using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms the dataset manager's commands (blueprint 11): creating a dataset with its live version over an existing object or a new
/// managed table (OI-33, OI-36), and deleting only a dataset no frozen snapshot holds (D10) with the managed tables only it reads
/// (OI-33).
/// </summary>
public sealed class DatasetCommandsTests
{
    /// <summary>The tables and services.</summary>
    private readonly DatasetFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A dataset over an existing table gets a live version with a field for every column fields can read, and the engine reads it
    /// at once, so the semantic revision advances.
    /// </summary>
    /// <returns>A task that completes when the dataset is checked.</returns>
    [Fact]
    public async Task CreateFromExisting_GivesTheLiveVersionAFieldPerReadableColumn()
    {
        var outcome = await _fixture.Commands().CreateAsync(Existing("bank.loan_snapshots"), Token);

        var version = Assert.Single(outcome.Value!.Versions);
        Assert.Equal((true, SnapshotRows.LiveId.ToString(System.Globalization.CultureInfo.InvariantCulture)), (version.IsLive, version.SnapshotId));
        Assert.Equal(version.Id, outcome.Value.LiveVersionId);
        Assert.Equal(["attributes", "balance", "branch_code", "loan_id"], _fixture.Fields.All.Select(field => field.SourceColumn).Order(StringComparer.Ordinal));
        Assert.Equal(DatasetFixture.StartingRevision + 1, _fixture.Revision);
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

    /// <summary>
    /// A frozen snapshot is read-only, so a dataset it holds a version of is not deleted: the delete check names the snapshot with
    /// <c>referenced_by_snapshot</c>, the delete is refused, and nothing is written.
    /// </summary>
    /// <returns>A task that completes when the check and refusal are checked.</returns>
    [Fact]
    public async Task DeleteDataset_IsBlockedByAFrozenSnapshot()
    {
        var check = await _fixture.Queries().DatasetDeleteCheckAsync(DatasetFixture.DatasetId, Token);
        var refusal = await _fixture.Commands().DeleteAsync(DatasetFixture.DatasetId, Token);

        Assert.False(check.Allowed);
        Assert.Equal(DeleteBlockerCodes.ReferencedBySnapshot, Assert.Single(check.Reasons).Code);
        Assert.Equal(RefusalKind.DeleteBlocked, refusal!.Kind);
        Assert.Equal(2, _fixture.Versions.All.Count);
    }

    /// <summary>
    /// A managed table exists only for the datasets over it (OI-33): deleting the last dataset that reads it drops its materialized
    /// view and the table and deletes its registry row, so no orphaned table or registry entry outlives the metadata.
    /// </summary>
    /// <returns>A task that completes when the drops are checked.</returns>
    [Fact]
    public async Task DeleteDataset_DropsTheManagedTableOnlyItReads()
    {
        await DeletableOverTheManagedTableAsync();
        await _fixture.Views.DefineAsync(
            new MaterializedViewDefinition { ViewName = MaterializedViewNames.ViewOf(DatasetFixture.ManagedTable), SourceObject = DatasetFixture.ManagedTable, Columns = [] },
            Token);

        Assert.Null(await _fixture.Commands().DeleteAsync(DatasetFixture.DatasetId, Token));

        Assert.Equal([$"drop {DatasetFixture.ManagedTable}"], _fixture.ManagedTables.Calls);
        Assert.Empty(_fixture.Views.Definitions);
        Assert.Empty(_fixture.Managed.All);
        Assert.Empty(_fixture.Datasets.All);
    }

    /// <summary>A managed table another dataset still reads is that dataset's source: the delete keeps the table and its registry row.</summary>
    /// <returns>A task that completes when the table is checked.</returns>
    [Fact]
    public async Task DeleteDataset_KeepsAManagedTableAnotherDatasetReads()
    {
        await DeletableOverTheManagedTableAsync();
        await _fixture.Versions.InsertAsync(DatasetRows.Version(200) with { DatasetId = 11, SourceObject = DatasetFixture.ManagedTable }, Token);

        Assert.Null(await _fixture.Commands().DeleteAsync(DatasetFixture.DatasetId, Token));

        Assert.Empty(_fixture.ManagedTables.Calls);
        Assert.Single(_fixture.Managed.All);
    }

    /// <summary>
    /// DDL cannot be rolled back, so a delete that stopped after dropping the table is completed by a retry: the drops are planned
    /// from what the database still holds, and the retry deletes the registry row without dropping the table again.
    /// </summary>
    /// <returns>A task that completes when the retry is checked.</returns>
    [Fact]
    public async Task DeleteDataset_AfterTheTableIsGone_DeletesOnlyTheRows()
    {
        await DeletableOverTheManagedTableAsync();
        _fixture.Catalog.Remove(DatasetFixture.ManagedTable);

        Assert.Null(await _fixture.Commands().DeleteAsync(DatasetFixture.DatasetId, Token));

        Assert.Empty(_fixture.ManagedTables.Calls);
        Assert.Empty(_fixture.Managed.All);
    }

    /// <summary>Leaves the dataset with only its live version, which reads the managed table, so the dataset can be deleted.</summary>
    private async Task DeletableOverTheManagedTableAsync()
    {
        await _fixture.Versions.DeleteAsync(new { DatasetVersionId = DatasetFixture.FrozenVersionId }, Token);
        await _fixture.UseManagedTableAsync();
    }

    /// <summary>A dataset over <c>loan_daily</c>.</summary>
    private static DatasetCreateFromExistingInput Existing(string key)
    {
        return new DatasetCreateFromExistingInput { Key = key, DisplayName = key, SourceObjectKind = SourceObjectKinds.Table, SourceObject = DatasetFixture.LoanTable };
    }
}
