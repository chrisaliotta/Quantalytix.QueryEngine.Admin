using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms against the development tenant that the seeded reports, which the engine's golden report cases run, pass every report
/// rule and read into a whole document, and that their sources' bridges are listed as report bridges owned by those sources. The
/// rules and the document read the report tables through the StarRocks gateways, and only a live read shows that they map onto the
/// seed's rows; a rule that refused a report the engine runs would keep it from being enabled. A copy of the margin statement also
/// round-trips through the report change set, the report bridges and the tenant's real publication rules, and is deleted again. Opt
/// in with <c>QE_ADMIN_LIVE_GATEWAY=true</c>.
/// </summary>
public sealed class ReportLiveTests
{
    /// <summary>Whether the live read is enabled.</summary>
    public static bool IsEnabled => LiveTenant.IsEnabled;

    /// <summary>Every enabled seeded report passes its rules and reads into a document, and its bridges are report bridges.</summary>
    /// <returns>A task that completes when the reports are checked.</returns>
    [Fact(SkipUnless = nameof(IsEnabled), Skip = "Set QE_ADMIN_LIVE_GATEWAY=true to read the seeded reports of tenant development.")]
    public async Task SeededReports_PassTheirRulesAndReadIntoADocument()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = LiveTenant.Services();
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ReportQueries>();
        var enabled = (await queries.ListAsync(cancellationToken)).Where(report => report.IsEnabled).ToList();

        Assert.Contains(enabled, report => report.Key == "bank.margin_statement");
        foreach (var report in enabled)
        {
            Assert.True((await queries.DiagnosticsAsync(report.Id, cancellationToken)).Ready);
            var document = await queries.DocumentAsync(report.Id, cancellationToken);
            Assert.NotEmpty(document.Rows);
            Assert.NotEmpty(document.Sources);
        }

        var marginStatement = await queries.DocumentAsync(enabled.Single(report => report.Key == "bank.margin_statement").Id, cancellationToken);
        var reportBridges = await scope.ServiceProvider.GetRequiredService<BridgeQueries>()
            .ListAsync(new BridgeListQuery { DimensionId = marginStatement.ReportDimensionId }, cancellationToken);
        Assert.All(reportBridges, bridge => Assert.NotNull(bridge.Owner));
        Assert.Contains(reportBridges, bridge => bridge.Owner!.SourceKey == "gl_daily");
    }

    /// <summary>
    /// A copy of the margin statement is created with its dimension published and pinned through the tenant's real publication
    /// rules, and takes the margin statement's rows, sources and rules through one report change set: its bridge sources get bridges
    /// of their own whose rules, including folder rules and -1 exclusions, round-trip through StarRocks. The copy passes its rules when
    /// enabled, and leaves nothing behind when deleted: its dimension, its bridges and their pins go with it.
    /// </summary>
    /// <returns>A task that completes when the round trip is checked and the copy is deleted.</returns>
    [Fact(SkipUnless = nameof(IsEnabled), Skip = "Set QE_ADMIN_LIVE_GATEWAY=true to round-trip a report through tenant development.")]
    public async Task CopiedReport_RoundTripsThroughStarRocks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = LiveTenant.Services();
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var queries = provider.GetRequiredService<ReportQueries>();
        var commands = provider.GetRequiredService<ReportCommands>();
        var pins = provider.GetRequiredService<AssumptionPins>();
        var margin = (await queries.ListAsync(cancellationToken)).Single(report => report.Key == "bank.margin_statement");
        var template = await queries.DocumentAsync(margin.Id, cancellationToken);
        var input = new ReportCreateInput
        {
            Key = $"zz_live_{DateTime.UtcNow:yyyyMMddHHmmss}",
            DisplayName = "Live round trip",
            ReportType = "test",
            ModelId = margin.ModelId,
            CopyFromReportId = margin.Id
        };

        var created = await commands.CreateAsync(input, cancellationToken);
        Assert.True(created.Succeeded);
        var copy = await queries.DocumentAsync(created.Value.Id, cancellationToken);
        var active = await pins.ActiveAsync(cancellationToken);
        try
        {
            Assert.False(copy.IsEnabled);
            Assert.Contains(await pins.ListAsync(active.AssumptionSetId, cancellationToken), pin => pin.IdentityId == copy.ReportDimensionId);
            Assert.Equal(template.Rows.Select(row => row.Key), copy.Rows.Select(row => row.Key));
            foreach (var source in template.Sources.Where(source => source.Method == ReportMappingMethods.Bridge))
            {
                var copied = copy.Sources.Single(other => other.Key == source.Key);
                Assert.NotEqual(source.BridgeId, copied.BridgeId);
                Assert.Equal(source.Rules.Keys.Order(StringComparer.Ordinal), copied.Rules.Keys.Order(StringComparer.Ordinal));
                foreach (var (rowKey, rules) in source.Rules) Assert.Equal(rules, copied.Rules[rowKey]);
            }

            Assert.True((await commands.SetEnabledAsync(copy.Id, new ReportEnabledInput { IsEnabled = true }, cancellationToken)).Succeeded);
            Assert.True((await commands.SetEnabledAsync(copy.Id, new ReportEnabledInput { IsEnabled = false }, cancellationToken)).Succeeded);
        }
        finally
        {
            await provider.GetRequiredService<ReportRemover>().DeleteAsync(copy.Id, cancellationToken);
        }

        var bridges = await provider.GetRequiredService<BridgeQueries>().ListAsync(new BridgeListQuery { DimensionId = copy.ReportDimensionId }, cancellationToken);
        Assert.DoesNotContain(await queries.ListAsync(cancellationToken), other => other.Id == copy.Id);
        Assert.Empty(await provider.GetRequiredService<ITableGateway<DimensionRecord>>().ListAsync(new { DimensionId = copy.ReportDimensionId }, cancellationToken));
        Assert.Empty(bridges);
        Assert.DoesNotContain(await pins.ListAsync(active.AssumptionSetId, cancellationToken), pin => pin.IdentityId == copy.ReportDimensionId);
    }
}
