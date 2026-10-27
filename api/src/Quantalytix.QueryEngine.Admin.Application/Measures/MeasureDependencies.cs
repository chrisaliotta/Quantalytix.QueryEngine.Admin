using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Derives <c>qe_measure_dependency</c> from a version's formulas (D7): one edge per measure of the version a formula names by key.
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
                .Where(measure => measure.MeasureKind == MeasureKinds.Formula)
                .SelectMany(formula => ExpressionInspector.Inspect(formula.ExpressionText!).MeasureKeys
                    .SelectMany(key => idsByKey[key])
                    .Distinct()
                    .Select(dependencyId => new MeasureDependencyRecord { MeasureId = formula.MeasureId, DependencyMeasureId = dependencyId }))
        ];
    }
}
