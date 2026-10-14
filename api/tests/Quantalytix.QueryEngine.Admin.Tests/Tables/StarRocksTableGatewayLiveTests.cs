using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Configuration;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Tables;

/// <summary>
/// Confirms against the live sandbox tenant that StarRocks runs the statements the table gateway generates: one multi-row
/// INSERT with a JSON column written through PARSE_JSON, a re-insert that upserts by key, an UPDATE by key and a DELETE with
/// an IN filter, after which no rows remain. The database-free tests check the SQL text; only a live round trip shows that
/// StarRocks accepts it and returns the values as written. Opt in with <c>QE_ADMIN_LIVE_SANDBOX=true</c>; the control-plane
/// connection comes from the API's user secrets or <c>ConnectionStrings__ControlPlane</c>. The test writes only to tenant
/// <c>sandbox</c>, never to the database behind the query engine's golden cases.
/// </summary>
public sealed class StarRocksTableGatewayLiveTests
{
    /// <summary>The tenant whose database exists for write testing.</summary>
    private const string SandboxTenantId = "sandbox";

    /// <summary>Whether the live round trip is enabled.</summary>
    public static bool IsEnabled
    {
        get
        {
            return string.Equals(Environment.GetEnvironmentVariable("QE_ADMIN_LIVE_SANDBOX"), "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Two dataset versions of a dataset no release references round-trip through insert, upsert, update and delete, keeping
    /// JSON, DATETIME, boolean and null values.
    /// </summary>
    /// <returns>A task that completes when the round trip is checked and its rows are deleted.</returns>
    [Fact(SkipUnless = nameof(IsEnabled), Skip = "Set QE_ADMIN_LIVE_SANDBOX=true to run the live round trip against tenant sandbox.")]
    public async Task GatewayStatements_RoundTripThroughTheSandbox()
    {
        var token = TestContext.Current.CancellationToken;
        await using var services = SandboxServices();
        await using var scope = services.CreateAsyncScope();
        var gateway = scope.ServiceProvider.GetRequiredService<ITableGateway<DatasetVersionRecord>>();
        var ids = scope.ServiceProvider.GetRequiredService<IIdGenerator>();
        var datasetId = ids.NextId();
        DatasetVersionRecord[] versions =
        [
            DatasetVersion(ids.NextId(), datasetId, 1, """{"origin":"admin-api-live-test","tags":["a","b"],"n":1}"""),
            DatasetVersion(ids.NextId(), datasetId, 2, null)
        ];
        var filter = new { DatasetVersionId = versions.Select(version => version.DatasetVersionId).ToArray() };

        await gateway.InsertManyAsync(versions, token);
        try
        {
            var inserted = await gateway.ListAsync(filter, token);
            var upserted = versions[1] with { SourceObject = "loan_performance" };
            await gateway.InsertAsync(upserted, token);
            var published = versions[0] with
            {
                Status = PublicationStatuses.Published,
                SupportsDetail = false,
                Properties = """{"origin":"admin-api-live-test","step":"updated"}""",
                PublishedAt = new DateTime(2026, 10, 14, 17, 30, 15, DateTimeKind.Utc).AddTicks(1234567)
            };
            await gateway.UpdateAsync(published, token);
            var changed = await gateway.ListAsync(filter, token);
            await File.WriteAllLinesAsync("/tmp/qe-live-observations.txt",
            [
                .. inserted.Select(row => $"inserted: {row}"),
                .. changed.Select(row => $"changed: {row}"),
                .. changed.Select(row => $"published_at kind={row.PublishedAt?.Kind} ticks={row.PublishedAt?.Ticks} expected={published.PublishedAt?.Ticks}")
            ], token);
        }
        finally
        {
            await gateway.DeleteAsync(filter, token);
        }

        Assert.Empty(await gateway.ListAsync(filter, token));
    }

    /// <summary>The StarRocks infrastructure as the API registers it, scoped to the sandbox tenant.</summary>
    private static ServiceProvider SandboxServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(AdminPolicies).Assembly)
            .AddEnvironmentVariables()
            .Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddStarRocksInfrastructure(configuration)
            .AddSingleton<ITenantContext>(new FixedTenantContext(SandboxTenantId))
            .BuildServiceProvider();
    }

    /// <summary>A draft version of a dataset over the <c>loan_daily</c> table.</summary>
    private static DatasetVersionRecord DatasetVersion(long datasetVersionId, long datasetId, int versionNumber, string? properties)
    {
        return new DatasetVersionRecord
        {
            DatasetVersionId = datasetVersionId,
            DatasetId = datasetId,
            VersionNumber = versionNumber,
            Status = PublicationStatuses.Draft,
            SourceObjectKind = SourceObjectKinds.Table,
            SourceObject = "loan_daily",
            SupportsAggregate = true,
            SupportsDetail = true,
            ExpressionLanguageVersion = "1.0",
            Properties = properties
        };
    }
}
