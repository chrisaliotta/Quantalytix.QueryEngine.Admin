using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Measures;

/// <summary>
/// Confirms how <see cref="MeasureDependencies"/> turns a version's formulas into <c>qe_measure_dependency</c> rows: each
/// <c>[m.key]</c> resolves to the version's measure with that key, a key referenced twice gives one row, and a key that names no
/// measure gives none. The query engine finds the measures a formula needs through these rows, so a missing row breaks the formula
/// at query time.
/// </summary>
public sealed class MeasureDependenciesTests
{
    /// <summary>The version whose dependencies are derived.</summary>
    private const long VersionId = 100;

    /// <summary>A formula's references resolve to the ids of the version's measures.</summary>
    [Fact]
    public void References_ResolveToMeasureIds()
    {
        var rows = MeasureDependencies.Of(
        [
            DatasetRows.Measure(1, VersionId, "interest", MeasureKinds.Aggregate),
            DatasetRows.Measure(2, VersionId, "balance", MeasureKinds.Aggregate),
            Formula(3, "yield", "[m.interest] / [m.balance]")
        ]);

        Assert.Equal([Row(3, 1), Row(3, 2)], Sorted(rows));
    }

    /// <summary>A measure referenced twice by one formula gives one row.</summary>
    [Fact]
    public void RepeatedReferences_GiveOneRow()
    {
        var rows = MeasureDependencies.Of([DatasetRows.Measure(1, VersionId, "balance", MeasureKinds.Aggregate), Formula(3, "doubled", "[m.balance] + [m.balance]")]);

        Assert.Equal([Row(3, 1)], Sorted(rows));
    }

    /// <summary>A key that names no measure of the version gives no row; the formula rule reports it instead.</summary>
    [Fact]
    public void UnknownKeys_GiveNoRow()
    {
        var rows = MeasureDependencies.Of([DatasetRows.Measure(1, VersionId, "balance", MeasureKinds.Aggregate), Formula(3, "ratio", "[m.balance] / [m.noSuchMeasure]")]);

        Assert.Equal([Row(3, 1)], Sorted(rows));
    }

    /// <summary>A formula measure of the version.</summary>
    private static MeasureRecord Formula(long measureId, string measureKey, string expressionText)
    {
        return DatasetRows.Measure(measureId, VersionId, measureKey, MeasureKinds.Formula) with { ExpressionText = expressionText };
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
