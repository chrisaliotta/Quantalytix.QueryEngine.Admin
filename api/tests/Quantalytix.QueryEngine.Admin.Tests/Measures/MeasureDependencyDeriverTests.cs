using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Measures;

/// <summary>
/// Confirms how <see cref="MeasureDependencyDeriver"/> turns formulas into <c>qe_measure_dependency</c> rows: each
/// <c>[m.key]</c> resolves to the measure of the same version with that key, a key referenced twice gives one row, a key
/// that names no measure gives none, and deriving again rewrites the version's rows from the current formulas, including a
/// formula that named a measure before the measure existed. The query engine finds the measures a formula needs through
/// these rows, so a missing or stale row breaks the formula at query time.
/// </summary>
public sealed class MeasureDependencyDeriverTests
{
    /// <summary>The version whose dependencies are derived.</summary>
    private const long VersionId = 100;

    /// <summary>Another version of the same dataset, with measures of the same keys.</summary>
    private const long OtherVersionId = 200;

    /// <summary>A formula's references resolve to the IDs of the measures of its own version, not of another version.</summary>
    /// <returns>A task that completes when the rows are checked.</returns>
    [Fact]
    public async Task References_ResolveToMeasureIdsOfTheSameVersion()
    {
        var measures = new InMemoryTableGateway<MeasureRecord>(
            DatasetRows.Measure(1, VersionId, "interest", MeasureKinds.Aggregate),
            DatasetRows.Measure(2, VersionId, "balance", MeasureKinds.Aggregate),
            Formula(3, VersionId, "yield", "[m.interest] / [m.balance]"),
            DatasetRows.Measure(9, OtherVersionId, "interest", MeasureKinds.Aggregate));
        var dependencies = new InMemoryTableGateway<MeasureDependencyRecord>();

        var rows = await DeriveAsync(measures, dependencies);

        Assert.Equal([Row(3, 1), Row(3, 2)], Sorted(rows));
        Assert.Equal([Row(3, 1), Row(3, 2)], Sorted(dependencies.All));
    }

    /// <summary>A measure referenced twice by one formula gives one row.</summary>
    /// <returns>A task that completes when the rows are checked.</returns>
    [Fact]
    public async Task RepeatedReferences_GiveOneRow()
    {
        var measures = new InMemoryTableGateway<MeasureRecord>(
            DatasetRows.Measure(1, VersionId, "balance", MeasureKinds.Aggregate),
            Formula(3, VersionId, "doubled", "[m.balance] + [m.balance]"));

        Assert.Equal([Row(3, 1)], Sorted(await DeriveAsync(measures, new InMemoryTableGateway<MeasureDependencyRecord>())));
    }

    /// <summary>A key that names no measure of the version gives no row; the formula rule reports it instead.</summary>
    /// <returns>A task that completes when the rows are checked.</returns>
    [Fact]
    public async Task UnknownKeys_GiveNoRow()
    {
        var measures = new InMemoryTableGateway<MeasureRecord>(
            DatasetRows.Measure(1, VersionId, "balance", MeasureKinds.Aggregate),
            Formula(3, VersionId, "ratio", "[m.balance] / [m.noSuchMeasure]"),
            DatasetRows.Measure(9, OtherVersionId, "noSuchMeasure", MeasureKinds.Aggregate));

        Assert.Equal([Row(3, 1)], Sorted(await DeriveAsync(measures, new InMemoryTableGateway<MeasureDependencyRecord>())));
    }

    /// <summary>
    /// Deriving after a formula changed replaces the version's rows: the old reference's row is gone, the new one is there,
    /// and other versions' rows are untouched.
    /// </summary>
    /// <returns>A task that completes when the rows are checked.</returns>
    [Fact]
    public async Task ReplacingAFormula_RewritesItsRows()
    {
        var measures = new InMemoryTableGateway<MeasureRecord>(
            DatasetRows.Measure(1, VersionId, "interest", MeasureKinds.Aggregate),
            DatasetRows.Measure(2, VersionId, "balance", MeasureKinds.Aggregate),
            Formula(3, VersionId, "yield", "[m.balance] * 2"),
            DatasetRows.Measure(9, OtherVersionId, "interest", MeasureKinds.Aggregate),
            Formula(8, OtherVersionId, "yield", "[m.interest]"));
        var dependencies = new InMemoryTableGateway<MeasureDependencyRecord>(Row(3, 1), Row(8, 9));

        await DeriveAsync(measures, dependencies);

        Assert.Equal([Row(3, 2), Row(8, 9)], Sorted(dependencies.All));
    }

    /// <summary>
    /// A formula that named a measure before it existed gains its row once the measure is created and the version derived
    /// again, so the engine finds every measure the formula needs.
    /// </summary>
    /// <returns>A task that completes when the rows are checked.</returns>
    [Fact]
    public async Task MeasureCreatedAfterTheFormula_GainsItsRow()
    {
        var measures = new InMemoryTableGateway<MeasureRecord>(Formula(3, VersionId, "yield", "[m.interest] / [m.balance]"));
        var dependencies = new InMemoryTableGateway<MeasureDependencyRecord>();
        await DeriveAsync(measures, dependencies);
        Assert.Empty(dependencies.All);

        await measures.InsertAsync(DatasetRows.Measure(1, VersionId, "interest", MeasureKinds.Aggregate), TestContext.Current.CancellationToken);
        await DeriveAsync(measures, dependencies);

        Assert.Equal([Row(3, 1)], Sorted(dependencies.All));
    }

    /// <summary>Derives the dependencies of <see cref="VersionId"/>.</summary>
    private static Task<IReadOnlyList<MeasureDependencyRecord>> DeriveAsync(
        InMemoryTableGateway<MeasureRecord> measures,
        InMemoryTableGateway<MeasureDependencyRecord> dependencies)
    {
        return new MeasureDependencyDeriver(measures, dependencies).DeriveAsync(VersionId, TestContext.Current.CancellationToken);
    }

    /// <summary>A formula measure of a version.</summary>
    private static MeasureRecord Formula(long measureId, long versionId, string measureKey, string expressionText)
    {
        return DatasetRows.Measure(measureId, versionId, measureKey, MeasureKinds.Formula) with { ExpressionText = expressionText };
    }

    /// <summary>A dependency row.</summary>
    private static MeasureDependencyRecord Row(long measureId, long dependencyMeasureId)
    {
        return new MeasureDependencyRecord { MeasureId = measureId, DependencyMeasureId = dependencyMeasureId };
    }

    /// <summary>Rows in a fixed order, for comparison.</summary>
    private static MeasureDependencyRecord[] Sorted(IEnumerable<MeasureDependencyRecord> rows)
    {
        return [.. rows.OrderBy(row => row.MeasureId).ThenBy(row => row.DependencyMeasureId)];
    }
}
