using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms the materialization commands of JSON path fields (OI-32): a field's column joins its source object's materialized view,
/// which every change redefines from the columns the fields name; the last column out drops the view; only JSON path fields of the
/// live version take part; and each change advances the semantic revision, because the engine reads
/// <c>materialized_column</c>.
/// </summary>
public sealed class DatasetMaterializationsTests
{
    /// <summary>The JSON path field of the live version.</summary>
    private const long FieldId = 5;

    /// <summary>The tables and services.</summary>
    private readonly DatasetFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The view of the loan table.</summary>
    private static string View => MaterializedViewNames.ViewOf(DatasetFixture.LoanTable);

    /// <summary>Materializing names the field's view column, defines the view with it, and answers the view's building state.</summary>
    /// <returns>A task that completes when the field is checked.</returns>
    [Fact]
    public async Task Materialize_DefinesTheViewWithTheFieldsColumn()
    {
        await SeedJsonFieldAsync();

        var outcome = await _fixture.Materializations().MaterializeAsync(DatasetFixture.LiveVersionId, FieldId, Token);

        Assert.Equal(MaterializationStates.Building, outcome.Value!.Materialization!.State);
        Assert.Equal("attributes_risk_rating_string", _fixture.Fields.All.Single().MaterializedColumn);
        Assert.Equal("$.risk.rating", Assert.Single(_fixture.Views.Definitions[View].Columns).JsonPath);
        Assert.Equal(DatasetFixture.StartingRevision + 1, _fixture.Revision);
    }

    /// <summary>Dematerializing the view's last column drops the view, and the field reads its JSON path again.</summary>
    /// <returns>A task that completes when the field and view are checked.</returns>
    [Fact]
    public async Task Dematerialize_LastColumn_DropsTheView()
    {
        await SeedJsonFieldAsync();
        var materializations = _fixture.Materializations();
        await materializations.MaterializeAsync(DatasetFixture.LiveVersionId, FieldId, Token);

        var outcome = await materializations.DematerializeAsync(DatasetFixture.LiveVersionId, FieldId, Token);

        Assert.Null(outcome.Value!.Materialization);
        Assert.Null(_fixture.Fields.All.Single().MaterializedColumn);
        Assert.False(_fixture.Views.Definitions.ContainsKey(View));
    }

    /// <summary>Only JSON path fields are materialized, and only on the live version: a frozen snapshot is read-only.</summary>
    /// <returns>A task that completes when the refusals are checked.</returns>
    [Fact]
    public async Task OtherFieldsAndFrozenVersions_AreConflicts()
    {
        await _fixture.Fields.InsertAsync(DatasetRows.ColumnField(6, DatasetFixture.LiveVersionId, "balance", "balance"), Token);
        await SeedJsonFieldAsync();
        var live = _fixture.Versions.All.Single(version => version.DatasetVersionId == DatasetFixture.LiveVersionId);

        var column = await _fixture.Materializations().MaterializeAsync(DatasetFixture.LiveVersionId, 6, Token);
        await _fixture.Versions.UpdateAsync(live with { SnapshotId = SnapshotRows.FrozenId }, Token);
        var frozen = await _fixture.Materializations().MaterializeAsync(DatasetFixture.LiveVersionId, FieldId, Token);

        Assert.Equal(RefusalKind.Conflict, column.Refusal!.Kind);
        Assert.Equal(RefusalKind.Conflict, frozen.Refusal!.Kind);
        Assert.Empty(_fixture.Views.Definitions);
    }

    /// <summary>A refresh needs a materialized field, and then starts a refresh of its view.</summary>
    /// <returns>A task that completes when the refreshes are checked.</returns>
    [Fact]
    public async Task Refresh_NeedsAMaterializedField()
    {
        await SeedJsonFieldAsync();
        var materializations = _fixture.Materializations();

        var before = await materializations.RefreshAsync(DatasetFixture.LiveVersionId, FieldId, Token);
        await materializations.MaterializeAsync(DatasetFixture.LiveVersionId, FieldId, Token);
        var after = await materializations.RefreshAsync(DatasetFixture.LiveVersionId, FieldId, Token);

        Assert.Equal(RefusalKind.Conflict, before.Refusal!.Kind);
        Assert.True(after.Succeeded);
        Assert.Equal([View], _fixture.Views.Refreshed);
    }

    /// <summary>Seeds a string JSON path field over <c>attributes</c> in the live version.</summary>
    private Task SeedJsonFieldAsync()
    {
        return _fixture.Fields.InsertAsync(
            DatasetRows.ColumnField(FieldId, DatasetFixture.LiveVersionId, "rating", "attributes") with { FieldSourceKind = FieldSourceKinds.JsonPath, JsonPath = "$.risk.rating" },
            Token);
    }
}
