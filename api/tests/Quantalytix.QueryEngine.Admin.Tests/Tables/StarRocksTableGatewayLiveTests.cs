using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Tables;

/// <summary>
/// Confirms against the development tenant that StarRocks runs the statements the table gateway generates and returns the
/// values as written: one multi-row INSERT whose rows hold a JSON column written through PARSE_JSON, a null JSON column and
/// both boolean values; a re-insert of an existing key, which replaces that row; an UPDATE by key that changes the status, a
/// boolean, the JSON column and the publication time, which reads back as UTC; and a DELETE with an IN filter, after which no
/// rows remain. The database-free tests check the SQL text; only a live round trip shows that StarRocks accepts it and that
/// every column type maps back onto the record. Opt in with <c>QE_ADMIN_LIVE_GATEWAY=true</c>; the control-plane connection
/// comes from the API's user secrets or <c>ConnectionStrings__ControlPlane</c>. The rows belong to a new dataset ID that no
/// release pins, and the test deletes them, so the query engine and its golden cases never read them.
/// </summary>
public sealed class StarRocksTableGatewayLiveTests
{
    /// <summary>The JSON the insert writes to the first row's <c>properties</c> column.</summary>
    private const string InsertedProperties = """{"origin":"admin-api-live-test","tags":["a","b"],"n":1}""";

    /// <summary>The JSON the update writes to the first row's <c>properties</c> column.</summary>
    private const string UpdatedProperties = """{"origin":"admin-api-live-test","step":"updated"}""";

    /// <summary>
    /// The publication time the update writes. Its fraction is below half a second, so the stored value keeps the same whole
    /// second whether the column truncates or rounds sub-second digits.
    /// </summary>
    private static readonly DateTime PublicationTime = new DateTime(2026, 10, 14, 17, 30, 15, DateTimeKind.Utc).AddTicks(1234567);

    /// <summary>Whether the live round trip is enabled.</summary>
    public static bool IsEnabled => LiveTenant.IsEnabled;

    /// <summary>
    /// Two dataset versions of a dataset no release references round-trip through insert, upsert, update and delete, keeping
    /// JSON, DATETIME, boolean and null values.
    /// </summary>
    /// <returns>A task that completes when the round trip is checked and its rows are deleted.</returns>
    [Fact(SkipUnless = nameof(IsEnabled), Skip = "Set QE_ADMIN_LIVE_GATEWAY=true to run the live round trip against tenant development.")]
    public async Task GatewayStatements_RoundTripThroughStarRocks()
    {
        var token = TestContext.Current.CancellationToken;
        await using var services = LiveTenant.Services();
        await using var scope = services.CreateAsyncScope();
        var gateway = scope.ServiceProvider.GetRequiredService<ITableGateway<DatasetVersionRecord>>();
        var ids = scope.ServiceProvider.GetRequiredService<IIdGenerator>();
        var datasetId = ids.NextId();
        var withJson = DatasetVersion(ids.NextId(), datasetId, 1, InsertedProperties);
        var withoutJson = DatasetVersion(ids.NextId(), datasetId, 2, null) with { SupportsDetail = false };
        var filter = new { DatasetVersionId = new[] { withJson.DatasetVersionId, withoutJson.DatasetVersionId } };

        await gateway.InsertManyAsync([withJson, withoutJson], token);
        try
        {
            var inserted = await RowsAsync(gateway, filter, token);
            Assert.Equal(2, inserted.Count);
            Assert.Equal(withJson with { Properties = null }, inserted[withJson.DatasetVersionId] with { Properties = null });
            AssertSameJson(InsertedProperties, inserted[withJson.DatasetVersionId].Properties);
            Assert.Equal(withoutJson, inserted[withoutJson.DatasetVersionId]);

            var upserted = withoutJson with { SourceObject = "loan_performance" };
            await gateway.InsertAsync(upserted, token);
            var afterUpsert = await RowsAsync(gateway, filter, token);
            Assert.Equal(2, afterUpsert.Count);
            Assert.Equal(upserted, afterUpsert[upserted.DatasetVersionId]);

            var published = withJson with
            {
                Status = PublicationStatuses.Published,
                SupportsDetail = false,
                Properties = UpdatedProperties,
                PublishedAt = PublicationTime
            };
            await gateway.UpdateAsync(published, token);
            var afterUpdate = await RowsAsync(gateway, filter, token);
            var updated = afterUpdate[published.DatasetVersionId];
            Assert.Equal(published with { Properties = null, PublishedAt = null }, updated with { Properties = null, PublishedAt = null });
            AssertSameJson(UpdatedProperties, updated.Properties);
            AssertSameSecond(PublicationTime, updated.PublishedAt);
            Assert.Equal(upserted, afterUpdate[upserted.DatasetVersionId]);
        }
        finally
        {
            await gateway.DeleteAsync(filter, token);
        }

        Assert.Empty(await gateway.ListAsync(filter, token));
    }

    /// <summary>The rows a filter matches, by dataset version ID.</summary>
    private static async Task<Dictionary<long, DatasetVersionRecord>> RowsAsync(
        ITableGateway<DatasetVersionRecord> gateway,
        object filter,
        CancellationToken cancellationToken)
    {
        var rows = await gateway.ListAsync(filter, cancellationToken);
        return rows.ToDictionary(row => row.DatasetVersionId);
    }

    /// <summary>
    /// Asserts that a JSON column read back holds the JSON value written. StarRocks may reformat JSON text, so the values are
    /// compared rather than the text.
    /// </summary>
    private static void AssertSameJson(string written, string? read)
    {
        Assert.NotNull(read);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(read)), $"Wrote {written} but read {read}.");
    }

    /// <summary>
    /// Asserts that a DATETIME read back is UTC and has the whole second written. Sub-second digits are not compared, because
    /// the column's sub-second precision on this cluster is not yet verified.
    /// </summary>
    private static void AssertSameSecond(DateTime written, DateTime? read)
    {
        var value = Assert.NotNull(read);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(WholeSeconds(written), WholeSeconds(value));
    }

    /// <summary>A time without its fraction of a second.</summary>
    private static DateTime WholeSeconds(DateTime value)
    {
        return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
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
