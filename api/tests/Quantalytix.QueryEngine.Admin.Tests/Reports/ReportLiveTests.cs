using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms against the development tenant that the seeded reports, which the engine's golden report cases run, pass every report
/// rule and open in the report editor, and that their sources' bridges are listed as report bridges owned by those sources. The
/// rules and the editor read the report tables through the StarRocks gateways, and only a live read shows that they map onto the
/// seed's rows; a rule that refused a report the engine runs would keep it from being enabled. A new report also round-trips
/// through the editor's writes and the tenant's real publication rules, and is deleted again. Opt in with
/// <c>QE_ADMIN_LIVE_GATEWAY=true</c>.
/// </summary>
public sealed class ReportLiveTests
{
    /// <summary>Whether the live read is enabled.</summary>
    public static bool IsEnabled => LiveTenant.IsEnabled;

    /// <summary>Every enabled seeded report passes its rules and reads into the editor, and its bridges are report bridges.</summary>
    /// <returns>A task that completes when the reports are checked.</returns>
    [Fact(SkipUnless = nameof(IsEnabled), Skip = "Set QE_ADMIN_LIVE_GATEWAY=true to read the seeded reports of tenant development.")]
    public async Task SeededReports_PassTheirRulesAndOpenInTheEditor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = LiveTenant.Services();
        await using var scope = services.CreateAsyncScope();
        var statuses = scope.ServiceProvider.GetRequiredService<ReportStatusService>();
        var editor = scope.ServiceProvider.GetRequiredService<ReportEditorReader>();
        var enabled = (await scope.ServiceProvider.GetRequiredService<ReportService>().ListAsync(cancellationToken)).Where(report => report.IsEnabled).ToList();

        Assert.Contains(enabled, report => report.ReportKey == "bank.margin_statement");
        foreach (var report in enabled)
        {
            Assert.Empty(await statuses.CheckAsync(report.ReportId, cancellationToken));
            var view = await editor.ReadAsync(report.ReportId, cancellationToken);
            Assert.NotEmpty(view.Rows);
            Assert.NotEmpty(view.Sources);
        }

        var reportBridges = await scope.ServiceProvider.GetRequiredService<BridgeService>()
            .ListAsync(new BridgeListQuery { Purpose = DimensionPurposes.Report }, cancellationToken);
        Assert.All(reportBridges, bridge => Assert.NotNull(bridge.Owner));
        Assert.Contains(reportBridges, bridge => bridge.Owner!.SourceKey == "gl_daily");
    }

    /// <summary>
    /// A report built like the margin statement, with a direct and a bridge source, is created with its dimension published and
    /// pinned through the tenant's real publication rules, takes rows, sources, variables and a column, passes its rules when
    /// enabled, and leaves nothing behind when deleted: its dimension, its bridge and their pins go with it.
    /// </summary>
    /// <returns>A task that completes when the round trip is checked and the report is deleted.</returns>
    [Fact(SkipUnless = nameof(IsEnabled), Skip = "Set QE_ADMIN_LIVE_GATEWAY=true to round-trip a report through tenant development.")]
    public async Task NewReport_RoundTripsThroughStarRocks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = LiveTenant.Services();
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var reports = provider.GetRequiredService<ReportService>();
        var editor = provider.GetRequiredService<ReportEditorReader>();
        var pins = provider.GetRequiredService<AssumptionPins>();
        var margin = (await reports.ListAsync(cancellationToken)).Single(report => report.ReportKey == "bank.margin_statement");
        var template = await editor.ReadAsync(margin.ReportId, cancellationToken);
        var direct = template.Sources.First(source => source.Source.MappingMethod == ReportMappingMethods.Direct);
        var bridged = template.Sources.First(source => source.Source.MappingMethod == ReportMappingMethods.Bridge);
        var variable = direct.Variables[0];
        var input = new NewReportInput
        {
            ReportKey = $"zz_live_{DateTime.UtcNow:yyyyMMddHHmmss}",
            DisplayName = "Live round trip",
            ReportType = "test",
            ModelId = margin.ModelId
        };

        var report = await reports.CreateAsync(input, cancellationToken);
        var active = await pins.ActiveAsync(cancellationToken);
        try
        {
            Assert.False(report.IsEnabled);
            Assert.Contains(await pins.ListAsync(active.AssumptionSetId, cancellationToken), pin => pin.IdentityId == report.ReportDimensionId);
            Assert.Empty(await provider.GetRequiredService<ReportRowService>().ReplaceAsync(
                report.ReportId,
                new ReportRowsInput { Rows = [Row("total", ReportRowKinds.Group, null), Row("LOANS", ReportRowKinds.Data, "total")] },
                cancellationToken));
            var sources = provider.GetRequiredService<ReportSourceService>();
            Assert.Empty(await sources.CreateAsync(report.ReportId, Source("direct", direct, ReportMappingMethods.Direct), cancellationToken));
            Assert.Empty(await sources.CreateAsync(report.ReportId, Source("bridged", bridged, ReportMappingMethods.Bridge), cancellationToken));
            var directId = (await editor.ReadAsync(report.ReportId, cancellationToken)).Sources.Single(source => source.Source.SourceKey == "direct").Source.ReportSourceId;
            var supplied = new SourceVariableInput { VariableKey = variable.VariableKey, MeasureKey = variable.MeasureKey, SourceFilterJson = variable.SourceFilterJson };
            Assert.Empty(await provider.GetRequiredService<ReportSourceVariableService>()
                .ReplaceAsync(report.ReportId, directId, new SourceVariablesInput { Variables = [supplied] }, cancellationToken));
            var column = new ReportColumnInput { ColumnKey = "amount", ValueKind = ReportValueKinds.Variable, MetricKey = variable.VariableKey, IsVisible = true };
            Assert.Empty(await provider.GetRequiredService<ReportColumnService>()
                .ReplaceAsync(report.ReportId, new ReportColumnsInput { Columns = [column] }, cancellationToken));

            var statuses = provider.GetRequiredService<ReportStatusService>();
            Assert.Empty(await statuses.SetAsync(report.ReportId, new ReportStatusInput { IsEnabled = true }, cancellationToken));
            Assert.Empty(await statuses.SetAsync(report.ReportId, new ReportStatusInput { IsEnabled = false }, cancellationToken));
        }
        finally
        {
            await reports.DeleteAsync(report.ReportId, cancellationToken);
        }

        var bridges = await provider.GetRequiredService<BridgeService>().ListAsync(new BridgeListQuery { Purpose = DimensionPurposes.Report }, cancellationToken);
        Assert.DoesNotContain(await reports.ListAsync(cancellationToken), other => other.ReportId == report.ReportId);
        Assert.Empty(await provider.GetRequiredService<ITableGateway<DimensionRecord>>().ListAsync(new { DimensionId = report.ReportDimensionId }, cancellationToken));
        Assert.DoesNotContain(bridges, bridge => bridge.Bridge.TargetDimensionId == report.ReportDimensionId);
        Assert.DoesNotContain(await pins.ListAsync(active.AssumptionSetId, cancellationToken), pin => pin.IdentityId == report.ReportDimensionId);
    }

    /// <summary>A visible row that shows its values, labelled with its key.</summary>
    private static ReportRowInput Row(string key, string kind, string? parentKey)
    {
        return new ReportRowInput { Key = key, Label = key, Kind = kind, ParentKey = parentKey, SortOrder = 0, IsVisible = true, ShowsValues = true };
    }

    /// <summary>A source of the dataset, role or field and time field a margin statement source reads.</summary>
    private static NewReportSourceInput Source(string sourceKey, EditorSource template, string mappingMethod)
    {
        return new NewReportSourceInput
        {
            SourceKey = sourceKey,
            DisplayName = sourceKey,
            DatasetId = template.Source.DatasetId,
            MappingMethod = mappingMethod,
            RoleId = template.Source.RoleId,
            FieldKey = template.Source.FieldKey,
            TimeFieldKey = template.Source.TimeFieldKey
        };
    }
}
