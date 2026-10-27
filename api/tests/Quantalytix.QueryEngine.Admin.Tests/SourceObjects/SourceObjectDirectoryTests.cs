using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.SourceObjects;

/// <summary>
/// Confirms which tables and views datasets may read (blueprint 11, OI-33, OI-34): every object of the tenant database except the
/// query engine's own <c>qe_*</c> tables, materialized views and <c>vw_qe_*</c> views, sorted by name, each marked managed when the
/// registry holds it and carrying the statistics StarRocks reports; columns are found by kind and name, typed as fields, and marked
/// generated on managed tables. The pickers, the create validators and the <c>source_object_exists</c> rule all read this list,
/// so a metadata object can never become a source.
/// </summary>
public sealed class SourceObjectDirectoryTests
{
    /// <summary>A database with fact tables, a view, metadata objects and a managed table.</summary>
    private readonly FixedSourceObjectCatalog _catalog = new FixedSourceObjectCatalog()
        .With(SourceObjectKinds.Table, "qe_field")
        .With(SourceObjectKinds.Table, "loan_daily", ("loan_id", "bigint(20)"), ("sketch", "hll"))
        .With(SourceObjectKinds.View, "vw_qe_dataset_field")
        .With(SourceObjectKinds.Table, "qe_mv_loan_daily")
        .With(SourceObjectKinds.View, "deposits_current")
        .With(SourceObjectKinds.Table, "staging_loans", ("loan_id", "bigint(20)"));

    /// <summary>The registry holding <c>staging_loans</c>.</summary>
    private readonly InMemoryTableGateway<ManagedSourceObjectRecord> _managed = new(new ManagedSourceObjectRecord
    {
        ManagedSourceObjectId = 1,
        SourceObject = "staging_loans",
        SourceObjectKind = SourceObjectKinds.Table,
        CreatedByDatasetId = 10,
        CreatedAt = DateTime.UnixEpoch
    });

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Metadata objects are left out, the rest sorted by name, the managed table marked, and statistics attached when measured.</summary>
    /// <returns>A task that completes when the list is checked.</returns>
    [Fact]
    public async Task List_LeavesOutMetadataObjectsAndMarksManagedTables()
    {
        _catalog.Statistics["loan_daily"] = new DatasetSourceObjectStats { RowCount = 12, SizeBytes = 4096, MeasuredAt = DateTime.UnixEpoch };

        var objects = await new SourceObjectDirectory(_catalog, _managed).ListAsync(Token);

        Assert.Equal(["deposits_current", "loan_daily", "staging_loans"], objects.Select(item => item.Name));
        Assert.True(objects.Single(item => item.Name == "staging_loans").IsManaged);
        Assert.Equal(12, objects.Single(item => item.Name == "loan_daily").Stats!.RowCount);
        Assert.Null(objects.Single(item => item.Name == "deposits_current").Stats);
    }

    /// <summary>Columns are found only with the object's own kind; a metadata object has none to offer.</summary>
    /// <param name="kind">The kind asked for.</param>
    /// <param name="name">The name asked for.</param>
    /// <param name="found">Whether the directory finds it.</param>
    /// <returns>A task that completes when the answer is checked.</returns>
    [Theory]
    [InlineData(SourceObjectKinds.Table, "loan_daily", true)]
    [InlineData(SourceObjectKinds.View, "loan_daily", false)]
    [InlineData(SourceObjectKinds.Table, "qe_field", false)]
    [InlineData(SourceObjectKinds.Table, "loan_monthly", false)]
    public async Task Columns_MatchKindAndName(string kind, string name, bool found)
    {
        Assert.Equal(found, await new SourceObjectDirectory(_catalog, _managed).ColumnsAsync(kind, name, Token) is not null);
    }

    /// <summary>A column fields can read carries its field type, one they cannot read none; managed tables' columns are generated.</summary>
    /// <returns>A task that completes when the columns are checked.</returns>
    [Fact]
    public async Task Columns_AreTypedAndMarkedGeneratedOnManagedTables()
    {
        var directory = new SourceObjectDirectory(_catalog, _managed);

        var loans = (await directory.ColumnsAsync(SourceObjectKinds.Table, "loan_daily", Token))!;
        var staging = (await directory.ColumnsAsync(SourceObjectKinds.Table, "staging_loans", Token))!;

        Assert.Equal([DataTypes.Int64, null], loans.Select(column => column.MappedType));
        Assert.False(loans[0].IsGenerated);
        Assert.True(staging.Single().IsGenerated);
    }
}
