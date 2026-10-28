using System.Text.Json;
using System.Text.Json.Nodes;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the report change set the report editor saves through (blueprint 16, OI-46): edits apply in memory, a preview answers
/// the pending document with its diagnostics and writes nothing, and a commit writes each fact to the table that owns it: rows to the
/// report dimension's members, member options and overlay lines (OI-48), a bridge source's rules to its own report bridge through
/// the mappings module (OI-49), left-out items to their table (OI-51). The server performs the cascades of OI-56, and an enabled
/// report's change set passes every report rule or is refused with 422 (P2).
/// </summary>
public sealed class ReportChangeSetTests
{
    /// <summary>The report.</summary>
    private const long ReportId = ReportRows.ReportId;

    /// <summary>The tables and the module.</summary>
    private readonly ReportFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A preview places a new row among its siblings, diagnoses the pending report, and writes nothing.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Preview_ShowsThePendingDocumentWithDiagnosticsAndWritesNothing()
    {
        var preview = (await _fixture.Changes().PreviewAsync(ReportId, Request(SaveRow("DEPOSITS", ReportRowKinds.Data, "TOTAL", 1)), Token)).Value!;

        Assert.Equal(["TOTAL", "LOANS", "DEPOSITS", "CASH"], preview.State.Rows.Select(row => row.Key));
        Assert.Empty(preview.Violations);
        Assert.True(preview.Diagnostics.Ready);
        Assert.Equal(["1003"], preview.Diagnostics.Coverage["gl"].Unmapped);
        Assert.Equal("warn", preview.Diagnostics.Steps.Single(step => step.Step == "items").Status);
        Assert.DoesNotContain(_fixture.Members.All, member => member.MemberKey == "DEPOSITS");
    }

    /// <summary>A commit stores a new data row as an assignable member of the report dimension and rebuilds its closure.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_StoresAMemberRowAndRebuildsTheClosure()
    {
        var outcome = await _fixture.Changes().CommitAsync(ReportId, Request(SaveRow("DEPOSITS", ReportRowKinds.Data, "TOTAL", 2)), Token);

        Assert.True(outcome.Succeeded);
        var member = _fixture.Members.All.Single(row => row.MemberKey == "DEPOSITS");
        Assert.True(member.IsAssignable);
        Assert.Equal(StructureVersionId, member.DimensionVersionId);
        Assert.Contains(_fixture.Closure.All, row => row.DescendantDimensionMemberId == member.DimensionMemberId && row.Depth == 1);
    }

    /// <summary>A member row stays a member row (OI-48), so saving it as an overlay kind is refused as input.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task SaveRow_RefusesTurningAMemberRowIntoAnOverlayRow()
    {
        var refusal = (await _fixture.Changes().PreviewAsync(ReportId, Request(SaveRow("CASH", ReportRowKinds.Section, "TOTAL", 1)), Token)).Refusal!;

        Assert.Equal(RefusalKind.Invalid, refusal.Kind);
        Assert.Contains("operations[0].row.kind", refusal.Errors.Keys);
    }

    /// <summary>An operation naming a row the report does not have is a conflict: another author may have removed it.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DeleteRow_OfARowTheReportLacks_IsAConflict()
    {
        var refusal = (await _fixture.Changes().PreviewAsync(ReportId, Request(new ReportDeleteRowOperation { Key = "NOPE" }), Token)).Refusal!;

        Assert.Equal(RefusalKind.Conflict, refusal.Kind);
    }

    /// <summary>Deleting a row moves its children up in its place (OI-56).</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DeleteRow_MovesItsChildrenUp()
    {
        var preview = (await _fixture.Changes().PreviewAsync(
            ReportId,
            Request(
                SaveRow("ASSETS", ReportRowKinds.Group, "TOTAL", 0),
                new ReportMoveRowOperation { Key = "LOANS", To = new ReportRowPosition { ParentKey = "ASSETS", Index = 0 } },
                new ReportDeleteRowOperation { Key = "ASSETS" }),
            Token)).Value!;

        Assert.Equal("TOTAL", preview.State.Rows.Single(row => row.Key == "LOANS").ParentKey);
        Assert.Equal(["TOTAL", "LOANS", "CASH"], preview.State.Rows.Select(row => row.Key));
    }

    /// <summary>Deleting a data row removes the bridge rules into it from the source's report bridge, and the scopes naming it (OI-56).</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task DeleteRow_RemovesItsBridgeRulesAndScopes()
    {
        await _fixture.Scopes.InsertAsync(new ReportVariableScopeRecord { ReportId = ReportId, VariableKey = "balance", ScopeMemberKey = "LOANS", SelectionScope = SelectionScopes.Exact }, Token);

        var outcome = await _fixture.Changes().CommitAsync(ReportId, Request(new ReportDeleteRowOperation { Key = "LOANS" }), Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal(["CASH"], _fixture.BridgeMappings.All.Select(mapping => mapping.TargetMemberKey));
        Assert.Empty(_fixture.Scopes.All);
        Assert.DoesNotContain(_fixture.Members.All, member => member.MemberKey == "LOANS");
    }

    /// <summary>A new bridge source has no bridge in a preview; its commit creates the report bridge it owns, into the report dimension.</summary>
    /// <returns>A task that completes when the preview and commit are checked.</returns>
    [Fact]
    public async Task SaveSource_CreatesTheBridgeSourcesReportBridgeOnCommit()
    {
        var request = Request(new ReportSaveSourceOperation { Input = BridgeSource("gl2") });

        var preview = (await _fixture.Changes().PreviewAsync(ReportId, request, Token)).Value!;
        var commit = (await _fixture.Changes().CommitAsync(ReportId, request, Token)).Value!;

        Assert.Null(preview.State.Sources.Single(source => source.Key == "gl2").BridgeId);
        var bridgeId = commit.State.Sources.Single(source => source.Key == "gl2").BridgeId!.Value;
        var bridge = _fixture.Bridges.All.Single(row => row.BridgeId == bridgeId);
        Assert.Equal(("balance_sheet_gl2", ReportRows.ReportDimensionId, AccountDimensionId), (bridge.BridgeKey, bridge.TargetDimensionId, bridge.SourceDimensionId));
    }

    /// <summary>A new source can start as a copy of another's variables and rules, which its own bridge stores on commit.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task SaveSource_CopiesAnotherSourcesRulesIntoItsOwnBridge()
    {
        var commit = (await _fixture.Changes().CommitAsync(ReportId, Request(new ReportSaveSourceOperation { Input = BridgeSource("gl2"), CopyFromSourceKey = "gl" }), Token)).Value!;

        var copy = commit.State.Sources.Single(source => source.Key == "gl2");
        Assert.Equal(["CASH", "LOANS"], copy.Rules.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["balance"], copy.Variables.Select(variable => variable.VariableKey));
        var version = _fixture.BridgeVersions.All.Single(row => row.BridgeId == copy.BridgeId);
        Assert.Equal(2, _fixture.BridgeMappings.All.Count(mapping => mapping.BridgeVersionId == version.BridgeVersionId));
    }

    /// <summary>Deleting a source deletes its report bridge and clears the profile picks that name it (OI-56).</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task DeleteSource_DeletesItsBridgeAndClearsItsPicks()
    {
        var outcome = await _fixture.Changes().CommitAsync(ReportId, Request(new ReportDeleteSourceOperation { Key = "gl" }), Token);

        Assert.True(outcome.Succeeded);
        Assert.Empty(_fixture.Sources.All);
        Assert.Empty(_fixture.Picks.All);
        Assert.DoesNotContain(_fixture.Bridges.All, bridge => bridge.BridgeId == BridgeId);
        Assert.Empty(_fixture.BridgeMappings.All);
    }

    /// <summary>A row's rules replace that row's mappings in the source's bridge; a folder rule takes every item under it (P7).</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task SetRowRules_ReplacesOneRowsMappings()
    {
        ReportSourceRule[] rules =
        [
            new() { ItemKey = "LENDING", Selection = SourceSelections.Descendants, Factor = 1m },
            new() { ItemKey = "1003", Selection = SourceSelections.Member, Factor = -1m }
        ];

        var commit = (await _fixture.Changes().CommitAsync(ReportId, Request(new ReportSetRowRulesOperation { SourceKey = "gl", RowKey = "LOANS", Rules = rules }), Token)).Value!;

        Assert.Equal(rules.OrderBy(rule => rule.ItemKey, StringComparer.Ordinal), commit.State.Sources.Single().Rules["LOANS"]);
        Assert.Equal("1002", _fixture.BridgeMappings.All.Single(mapping => mapping.TargetMemberKey == "CASH").SourceMemberKey);
        Assert.Equal(3, _fixture.BridgeMappings.All.Count);
    }

    /// <summary>Only data rows take items, so rules into a group row are refused as input.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task SetRowRules_IntoAGroupRow_IsRefused()
    {
        var refusal = (await _fixture.Changes().PreviewAsync(
            ReportId, Request(new ReportSetRowRulesOperation { SourceKey = "gl", RowKey = "TOTAL", Rules = [] }), Token)).Refusal!;

        Assert.Contains("operations[0].rowKey", refusal.Errors.Keys);
    }

    /// <summary>An item left out on purpose is not a coverage gap, and is stored in the left-out table (OI-51).</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task SetLeftOut_ExcludesTheItemFromCoverage()
    {
        var request = Request(new ReportSetLeftOutOperation { SourceKey = "gl", ItemKeys = ["1003"] });

        var coverage = (await _fixture.Changes().PreviewAsync(ReportId, request, Token)).Value!.Diagnostics.Coverage["gl"];
        await _fixture.Changes().CommitAsync(ReportId, request, Token);

        Assert.Empty(coverage.Unmapped);
        Assert.Equal(["1003"], coverage.LeftOut);
        Assert.Equal("1003", Assert.Single(_fixture.LeftOut.All).MemberKey);
    }

    /// <summary>
    /// Deleting a calculation dimension removes it from the default breakout and from the default filter (OI-56); the filter is stored
    /// in the engine's shape, whose grouping names its level <c>level</c>, and read back in the contract's, <c>levelKey</c> (OI-54).
    /// </summary>
    /// <returns>A task that completes when the commits are checked.</returns>
    [Fact]
    public async Task CalculationDimensions_CascadeIntoTheDefaults()
    {
        var filter = new FilterNode(JsonNode.Parse("""
            {"operator":"and","operands":[
              {"operator":"eq","left":{"type":"dimension","id":"account","grouping":{"mode":"level","levelKey":"group"}},"right":{"type":"literal","value":"x"}},
              {"operator":"eq","left":{"type":"dimension","id":"other"},"right":{"type":"literal","value":"y"}}]}
            """)!.AsObject());
        var saved = (await _fixture.Changes().CommitAsync(
            ReportId,
            Request(
                SaveDimension("account"),
                SaveDimension("other"),
                new ReportUpdateSettingsOperation { Patch = new ReportSettingsPatch { DefaultBreakout = ["account", "other"], DefaultFilter = filter } }),
            Token)).Value!;
        Assert.Contains("\"level\":\"group\"", _fixture.Reports.All.Single().DefaultFilterJson);
        Assert.Equal("group", saved.State.Settings.DefaultFilter!.Json["operands"]![0]!["left"]!["grouping"]!["levelKey"]!.GetValue<string>());

        var deleted = (await _fixture.Changes().CommitAsync(ReportId, Request(new ReportDeleteCalculationDimensionOperation { Key = "account" }), Token)).Value!;

        Assert.Equal(["other"], deleted.State.Settings.DefaultBreakout);
        Assert.Equal("other", deleted.State.Settings.DefaultFilter!.Json["operands"]!.AsArray().Single()!["left"]!["id"]!.GetValue<string>());
    }

    /// <summary>A style a row shows cannot be deleted: the change is refused with <c>report_styles_resolve</c> even while disabled.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DeleteStyle_ThatARowShows_IsRefused()
    {
        var style = new ReportStyle
        {
            Key = "bold", DisplayName = "Bold", DefaultRole = null, FontFamily = null, FontSize = null, FontWeight = FontWeights.Bold, IsItalic = false,
            TextColor = null, BackgroundColor = null, BorderTop = null, BorderBottom = null, Indent = null
        };
        var cash = SaveRow("CASH", ReportRowKinds.Data, "TOTAL", 1);
        await _fixture.Changes().CommitAsync(ReportId, Request(new ReportSaveStyleOperation { Style = style }, cash with { Row = cash.Row with { StyleKey = "bold" } }), Token);

        var refusal = (await _fixture.Changes().PreviewAsync(ReportId, Request(new ReportDeleteStyleOperation { Key = "bold" }), Token)).Refusal!;

        Assert.Equal(RefusalKind.RulesViolated, refusal.Kind);
        Assert.Equal(ReportStylesResolveRule.Code, Assert.Single(refusal.Violations).Code);
    }

    /// <summary>The default profile cannot be deleted: the change is refused with <c>default_profile_enabled</c>.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DeleteProfile_ThatIsTheDefault_IsRefused()
    {
        var refusal = (await _fixture.Changes().PreviewAsync(ReportId, Request(new ReportDeleteProfileOperation { Key = "base" }), Token)).Refusal!;

        Assert.Equal(RefusalKind.RulesViolated, refusal.Kind);
        Assert.Equal(DefaultProfileEnabledRule.Code, Assert.Single(refusal.Violations).Code);
    }

    /// <summary>A disabled report saves a change that breaks a rule, and the preview lists the finding, which keeps it from being enabled.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DisabledReport_TakesAChangeThatBreaksARule_AsAFinding()
    {
        var preview = (await _fixture.Changes().PreviewAsync(ReportId, Request(new ReportSetColumnsOperation { Columns = [] }), Token)).Value!;

        Assert.Equal(ColumnKeysDistinctRule.Code, Assert.Single(preview.Violations).Code);
        Assert.False(preview.Diagnostics.Ready);
        Assert.Equal("fail", preview.Diagnostics.Steps.Single(step => step.Step == "columns").Status);
    }

    /// <summary>An enabled report's change set that breaks a report rule is refused with 422, in preview and commit alike (P2).</summary>
    /// <returns>A task that completes when the preview and commit are checked.</returns>
    [Fact]
    public async Task EnabledReport_RefusesAChangeThatBreaksARule()
    {
        await _fixture.SetEnabledAsync(true);
        var request = Request(new ReportSetColumnsOperation { Columns = [] });

        var preview = (await _fixture.Changes().PreviewAsync(ReportId, request, Token)).Refusal!;
        var commit = (await _fixture.Changes().CommitAsync(ReportId, request, Token)).Refusal!;

        Assert.Equal(RefusalKind.RulesViolated, preview.Kind);
        Assert.Equal(ColumnKeysDistinctRule.Code, Assert.Single(commit.Violations).Code);
        Assert.Single(_fixture.Columns.All);
    }

    /// <summary>The engine reads an enabled report as it stands, so its commit advances the semantic revision; a disabled one's does not.</summary>
    /// <param name="isEnabled">Whether the report is enabled.</param>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Commit_AdvancesTheRevisionOnlyForAnEnabledReport(bool isEnabled)
    {
        await _fixture.SetEnabledAsync(isEnabled);

        await _fixture.Changes().CommitAsync(ReportId, Request(new ReportUpdateSettingsOperation { Patch = new ReportSettingsPatch { DisplayName = "Renamed" } }), Token);

        Assert.Equal("Renamed", _fixture.Reports.All.Single().DisplayName);
        Assert.Equal(isEnabled ? StartingRevision + 1 : StartingRevision, _fixture.SemanticRevisionNumber);
    }

    /// <summary>
    /// A profile resolves a variable it picks a supplying source for; a data row the source's bridge does not reach is a gap. The
    /// diagnostics group each rule's findings into its step.
    /// </summary>
    /// <returns>A task that completes when the diagnostics are checked.</returns>
    [Fact]
    public async Task Diagnostics_ResolveEachProfilesVariables()
    {
        var stored = await _fixture.Queries().DiagnosticsAsync(ReportId, Token);
        var pending = (await _fixture.Changes().PreviewAsync(ReportId, Request(new ReportSetRowRulesOperation { SourceKey = "gl", RowKey = "CASH", Rules = [] }), Token))
            .Value!.Diagnostics;

        Assert.Equal(("ok", "gl"), (stored.Resolution["base"]["balance"].Status, stored.Resolution["base"]["balance"].SourceKey));
        Assert.Equal(["CASH"], pending.Resolution["base"]["balance"].RowGaps);
        Assert.Equal("gap", pending.Resolution["base"]["balance"].Status);
    }

    /// <summary>A change set of the operations.</summary>
    private static ChangeSetRequest<ReportOperation> Request(params IReadOnlyList<ReportOperation> operations)
    {
        return new ChangeSetRequest<ReportOperation> { Operations = operations };
    }

    /// <summary>A visible row, labelled with its key, at an index under a parent.</summary>
    private static ReportSaveRowOperation SaveRow(string key, string kind, string? parentKey, int index)
    {
        return new ReportSaveRowOperation
        {
            Row = new ReportRow
            {
                Key = key,
                Label = key,
                Kind = kind,
                ParentKey = parentKey,
                IsVisible = true,
                ShowsValues = true,
                CalculatedMeasureKey = null,
                ValueColumnKey = null,
                StyleKey = null,
                Parameters = new Dictionary<string, JsonElement>()
            },
            Index = index
        };
    }

    /// <summary>A bridge source over GL's account role.</summary>
    private static ReportSourceInput BridgeSource(string key)
    {
        return new ReportSourceInput
        {
            Key = key,
            DisplayName = key,
            DatasetId = GlDatasetId,
            TimeFieldKey = "as_of_date",
            Method = ReportMappingMethods.Bridge,
            RoleId = AccountRoleId,
            FieldKey = null
        };
    }

    /// <summary>A groupable, filterable calculation dimension on the account role.</summary>
    private static ReportSaveCalculationDimensionOperation SaveDimension(string key)
    {
        return new ReportSaveCalculationDimensionOperation
        {
            Input = new ReportCalculationDimension
            {
                Key = key, DisplayName = key, RoleId = AccountRoleId, Grouping = new GroupingMember(), IsGroupable = true, IsFilterable = true, IsEnabled = true
            }
        };
    }
}
