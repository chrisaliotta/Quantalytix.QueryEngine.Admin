using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Derives the <c>qe_measure_dependency</c> rows of a dataset version from its formulas: every <c>[m.key]</c> a formula
/// references becomes one row naming the measure of the same version with that key, and keys that name no measure produce
/// no row (the <c>formula_references_known_measures</c> rule reports them). The query engine reads these rows to find the
/// measures a formula needs. A formula may name a measure created after it, so every formula of the version is derived
/// again whenever one of its measures is saved.
/// </summary>
public sealed class MeasureDependencyDeriver(ITableGateway<MeasureRecord> measures, ITableGateway<MeasureDependencyRecord> dependencies)
{
    /// <summary>Replaces the dependency rows of a dataset version's measures with the ones their formulas reference.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The version's dependency rows after the rewrite, one per formula and referenced measure.</returns>
    public async Task<IReadOnlyList<MeasureDependencyRecord>> DeriveAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionMeasures = await measures.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var idsByKey = versionMeasures.ToLookup(measure => measure.MeasureKey, measure => measure.MeasureId, StringComparer.Ordinal);
        MeasureDependencyRecord[] rows =
        [
            .. versionMeasures
                .Where(measure => measure.MeasureKind == MeasureKinds.Formula)
                .SelectMany(formula => ExpressionInspector.Inspect(formula.ExpressionText!).MeasureKeys
                    .SelectMany(key => idsByKey[key])
                    .Select(dependencyId => new MeasureDependencyRecord { MeasureId = formula.MeasureId, DependencyMeasureId = dependencyId }))
                .Distinct()
        ];
        await dependencies.DeleteAsync(new { MeasureId = versionMeasures.Select(measure => measure.MeasureId).ToArray() }, cancellationToken);
        await dependencies.InsertManyAsync(rows, cancellationToken);
        return rows;
    }
}
