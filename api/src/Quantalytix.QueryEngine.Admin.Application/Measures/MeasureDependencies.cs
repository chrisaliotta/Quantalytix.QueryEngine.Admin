using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Derives <c>qe_measure_dependency</c> from a version's formulas and compound rates (D7): one edge per measure of the version a
/// formula names by key.
/// A key that names no measure of the version has no edge; the <c>formula_references_known_measures</c> rule reports it.
/// </summary>
public static class MeasureDependencies
{
    /// <summary>The dependency edges of a version's measures.</summary>
    /// <param name="measures">Every measure of the version.</param>
    /// <returns>The edges, each once.</returns>
    public static IReadOnlyList<MeasureDependencyRecord> Of(IReadOnlyList<MeasureRecord> measures)
    {
        var idsByKey = measures.ToLookup(measure => measure.MeasureKey, measure => measure.MeasureId, StringComparer.Ordinal);
        return
        [
            .. measures
                .Where(measure => MeasureKinds.ComposesMeasures(measure.MeasureKind))
                .SelectMany(formula => ExpressionInspector.Inspect(formula.ExpressionText!).MeasureKeys
                    .SelectMany(key => idsByKey[key])
                    .Distinct()
                    .Select(dependencyId => new MeasureDependencyRecord { MeasureId = formula.MeasureId, DependencyMeasureId = dependencyId }))
        ];
    }

    /// <summary>The measures a measure's expression reaches, directly or through the expressions of the measures it reads.</summary>
    /// <param name="measureId">The measure.</param>
    /// <param name="edges">Each measure's dependencies, by measure.</param>
    /// <returns>The reached measures; the measure itself only when it is on a cycle.</returns>
    public static HashSet<long> Reachable(long measureId, ILookup<long, long> edges)
    {
        var reached = new HashSet<long>();
        var pending = new Stack<long>(edges[measureId]);
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            if (!reached.Add(next)) continue;
            foreach (var dependency in edges[next]) pending.Push(dependency);
        }

        return reached;
    }
}
