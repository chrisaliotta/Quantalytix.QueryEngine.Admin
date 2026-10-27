using System.Text.Json;
using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// Confirms how report rows carry styles and parameter values. The row tree is written whole, so a row's style goes on its member
/// option, made for a member that needs only a style, or on its overlay row, and the rows' values replace the report's, each stored
/// as a number or as text by its parameter's type, which the engine reads without checking. Values and styles that do not resolve are
/// refused at their path before anything is written, for disabled reports too, because the model gate checks every report's values.
/// A style a row shows cannot be deleted, and an enabled report takes one default style per role. Model 40 declares
/// <c>dayCount</c>, limited to the conventions, and <c>scale</c>, a number.
/// </summary>
public sealed class RowStylesAndParametersTests
{
    /// <summary>Disabled report 60 with rows TOTAL, LOANS and CASH.</summary>
    private readonly ReportEditorFixture _fixture = new();

    /// <summary>
    /// TOTAL's style and LOANS' and TOTAL's values are stored, TOTAL getting a member option for its style alone, and the section's
    /// style goes on its overlay row; the editor reads each row's style and own values back, with the model's parameters.
    /// </summary>
    /// <returns>A task that completes when the rows are written and read.</returns>
    [Fact]
    public async Task StylesAndValues_AreStoredByTypeAndReadBack()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareAsync();

        var violations = await _fixture.Rows().ReplaceAsync(ReportRows.ReportId, Tree(), cancellationToken);

        Assert.Empty(violations);
        var option = Assert.Single(_fixture.MemberOptions.All);
        Assert.Equal(("TOTAL", true, true, "subtotals"), (option.MemberKey, option.IsVisible, option.ShowsValues, option.StyleKey));
        Assert.Equal("notes", Assert.Single(_fixture.Lines.All).StyleKey);
        Assert.Equal(
            [("LOANS", "scale", (decimal?)1000.5m, (string?)null), ("TOTAL", "dayCount", null, DayCountConventions.Actual360)],
            _fixture.RowParameters.All.OrderBy(value => value.RowKey, StringComparer.Ordinal).Select(value => (value.RowKey, value.ParameterKey, value.NumberValue, value.TextValue)));
        var view = await _fixture.Reader().ReadAsync(ReportRows.ReportId, cancellationToken);
        var total = view.Rows.Single(row => row.Key == "TOTAL");
        Assert.Equal(("subtotals", DayCountConventions.Actual360), (total.StyleKey, total.Parameters["dayCount"].GetString()));
        Assert.Equal(1000.5m, view.Rows.Single(row => row.Key == "LOANS").Parameters["scale"].GetDecimal());
        Assert.Empty(view.Rows.Single(row => row.Key == "CASH").Parameters);
        Assert.Equal("notes", view.Rows.Single(row => row.Key == "NOTE").StyleKey);
        Assert.Equal(["dayCount", "scale"], view.Parameters.Select(parameter => parameter.ParameterKey));
    }

    /// <summary>A later tree replaces the report's values as a whole: values it no longer lists go, and so does TOTAL's style-only option.</summary>
    /// <returns>A task that completes when both trees are written.</returns>
    [Fact]
    public async Task Values_AreReplacedAsAWhole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareAsync();
        await _fixture.Rows().ReplaceAsync(ReportRows.ReportId, Tree(), cancellationToken);

        await _fixture.Rows().ReplaceAsync(
            ReportRows.ReportId,
            new ReportRowsInput
            {
                Rows =
                [
                    Row("TOTAL", ReportRowKinds.Group, null),
                    Row("LOANS", ReportRowKinds.Data, "TOTAL") with { Parameters = new Dictionary<string, JsonElement> { ["scale"] = Json(1m) } },
                    Row("CASH", ReportRowKinds.Data, "TOTAL")
                ]
            },
            cancellationToken);

        Assert.Equal([("LOANS", "scale", (decimal?)1m)], _fixture.RowParameters.All.Select(value => (value.RowKey, value.ParameterKey, value.NumberValue)));
        Assert.Empty(_fixture.MemberOptions.All);
    }

    /// <summary>
    /// A style the report lacks, a parameter the model lacks, a value of the other type, a text outside the allowed values and a number
    /// the column cannot hold are each refused at their path, so the row sheet shows them on their fields.
    /// </summary>
    /// <returns>A task that completes when the tree is validated.</returns>
    [Fact]
    public async Task UnresolvedStylesAndValues_AreRefusedAtTheirPaths()
    {
        await DeclareAsync();
        var input = new ReportRowsInput
        {
            Rows =
            [
                Row("TOTAL", ReportRowKinds.Group, null) with { StyleKey = "gone" },
                Row("LOANS", ReportRowKinds.Data, "TOTAL") with
                {
                    Parameters = new Dictionary<string, JsonElement> { ["margin"] = Json(1m), ["dayCount"] = Json(360m), ["scale"] = Json("x") }
                },
                Row("CASH", ReportRowKinds.Data, "TOTAL") with
                {
                    Parameters = new Dictionary<string, JsonElement> { ["dayCount"] = Json("monthly"), ["scale"] = Json(0.1234567890123m) }
                }
            ]
        };

        var result = await _fixture.RowsValidator().TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [("rows[0].styleKey", "Row 'TOTAL' shows style 'gone', which is no style of the report."),
             ("rows[1].parameters.margin", "Row 'LOANS' sets 'margin', which is no parameter of the report's model."),
             ("rows[1].parameters.dayCount", "Row 'LOANS' sets 'dayCount', which is a text parameter, so its value must be text."),
             ("rows[1].parameters.scale", "Row 'LOANS' sets 'scale', which is a number parameter, so its value must be a number."),
             ("rows[2].parameters.dayCount", "Row 'CASH' sets 'dayCount', which takes only 'actual_actual', 'actual_365', 'actual_360', 'thirty_360_us', 'thirty_e_360', not 'monthly'."),
             ("rows[2].parameters.scale", "Row 'CASH' sets 'scale', which takes numbers that fit DECIMAL(38, 12): at most 26 digits before the point and 12 after.")],
            result.Errors.Select(error => (error.PropertyName, error.ErrorMessage)));
    }

    /// <summary>
    /// A style a row shows is not deleted: on a disabled report the delete names the rows, and on an enabled report the report rules
    /// refuse it first. A style no row shows is deleted.
    /// </summary>
    /// <returns>A task that completes when the deletes are checked.</returns>
    [Fact]
    public async Task DeletingAStyleARowShows_IsRefused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareAsync();
        await _fixture.Rows().ReplaceAsync(ReportRows.ReportId, Tree(), cancellationToken);

        Assert.Equal("Style 'subtotals' cannot be deleted: shown by rows 'TOTAL'.", (await DeleteStyleAsync("subtotals")).Conflict);
        await _fixture.SetEnabledAsync(true);
        Assert.Equal([ReportStylesResolveRule.Code], (await DeleteStyleAsync("notes")).Violations.Select(violation => violation.Code));
        await _fixture.Styles.InsertAsync(Style("unused", null), cancellationToken);
        var unused = await DeleteStyleAsync("unused");
        Assert.Null(unused.Conflict);
        Assert.Empty(unused.Violations);
        Assert.Equal(["notes", "subtotals"], _fixture.Styles.All.Select(style => style.StyleKey).Order(StringComparer.Ordinal));
    }

    /// <summary>An enabled report refuses a second default style of a role, and keeps the first; a disabled report takes it until enabled.</summary>
    /// <returns>A task that completes when the writes are checked.</returns>
    [Fact]
    public async Task SecondDefaultOfARole_IsRefusedOnAnEnabledReport()
    {
        await _fixture.Styles.InsertAsync(Style("totals", RowRoles.Total), TestContext.Current.CancellationToken);
        await _fixture.SetEnabledAsync(true);

        var violations = await PutStyleAsync(Style("grand", RowRoles.Total));
        await _fixture.SetEnabledAsync(false);
        var disabled = await PutStyleAsync(Style("grand", RowRoles.Total));

        Assert.Equal(
            ["Report 'balance_sheet' styles 'grand', 'totals' are each the default of total rows; a role has at most one default style."],
            violations.Select(violation => violation.Message));
        Assert.Empty(disabled);
        Assert.Equal(["grand", "totals"], _fixture.Styles.All.Select(style => style.StyleKey).Order(StringComparer.Ordinal));
    }

    /// <summary>Declares model 40's parameters and stores report 60's styles <c>subtotals</c> and <c>notes</c>.</summary>
    private async Task DeclareAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Parameters.InsertManyAsync([ParameterRow(DayCount()), ParameterRow(NumberParameter("scale", 1m))], cancellationToken);
        await _fixture.Styles.InsertManyAsync([Style("subtotals", RowRoles.Subtotal), Style("notes", null)], cancellationToken);
    }

    /// <summary>
    /// The stored rows with TOTAL styled <c>subtotals</c> on actual/360, LOANS at scale 1000.5, CASH as it is, and a section NOTE
    /// under TOTAL styled <c>notes</c>.
    /// </summary>
    private static ReportRowsInput Tree()
    {
        return new ReportRowsInput
        {
            Rows =
            [
                Row("TOTAL", ReportRowKinds.Group, null) with
                {
                    StyleKey = "subtotals",
                    Parameters = new Dictionary<string, JsonElement> { ["dayCount"] = Json(DayCountConventions.Actual360) }
                },
                Row("LOANS", ReportRowKinds.Data, "TOTAL") with { Parameters = new Dictionary<string, JsonElement> { ["scale"] = Json(1000.5m) } },
                Row("CASH", ReportRowKinds.Data, "TOTAL"),
                Row("NOTE", ReportRowKinds.Section, "TOTAL") with { StyleKey = "notes" }
            ]
        };
    }

    /// <summary>Writes a style of report 60 through the report gate, as the styles controller does.</summary>
    private Task<IReadOnlyList<RuleViolation>> PutStyleAsync(ReportStyleRecord style)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        return _fixture.Changes().ApplyAsync(
            ReportRows.ReportId, report => ReportStyleService.Put(report, style), () => _fixture.StyleService().ReplaceAsync(style, cancellationToken), cancellationToken);
    }

    /// <summary>Deletes a style of report 60 through the report gate, as the styles controller does.</summary>
    private async Task<(string? Conflict, IReadOnlyList<RuleViolation> Violations)> DeleteStyleAsync(string styleKey)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? conflict = null;
        var violations = await _fixture.Changes().ApplyAsync(
            ReportRows.ReportId,
            report => ReportStyleService.Without(report, styleKey),
            async () => conflict = await _fixture.StyleService().DeleteAsync(ReportRows.ReportId, styleKey, cancellationToken),
            cancellationToken);
        return (conflict, violations);
    }

    /// <summary>A style of report 60 that is the default of a role, or of none.</summary>
    private static ReportStyleRecord Style(string styleKey, string? defaultRole)
    {
        return new ReportStyleRecord { ReportId = ReportRows.ReportId, StyleKey = styleKey, DisplayName = styleKey, DefaultRole = defaultRole, IsItalic = false };
    }

    /// <summary>A visible row that shows its values, labelled with its key.</summary>
    private static ReportRowInput Row(string key, string kind, string? parentKey)
    {
        return new ReportRowInput { Key = key, Label = key, Kind = kind, ParentKey = parentKey, SortOrder = 0, IsVisible = true, ShowsValues = true };
    }

    /// <summary>A value as the row tree sends it.</summary>
    private static JsonElement Json(object value)
    {
        return JsonSerializer.SerializeToElement(value);
    }
}
